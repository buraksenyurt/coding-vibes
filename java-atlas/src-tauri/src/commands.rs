//! `#[tauri::command]` functions: the only doors from the WebView into Rust.
//!
//! Scanning moves to a worker thread with `spawn_blocking`, so the window
//! stays responsive; progress flows back through a `Channel`.

use std::path::PathBuf;
use std::sync::Arc;

use serde::Serialize;
use tauri::ipc::Channel;
use tauri::{AppHandle, State};

use crate::dto::{
    AtlasViewDto, EvidenceDto, NodeDetailsDto, ProjectSummary, RecentProject, ScanEvent,
    SearchHitDto, TreeDto, ViewRequest,
};
use crate::error::{AppError, ErrorKind};
use crate::project::{self, OpenProject};
use crate::recent;
use crate::state::AppState;

/// What the status bar shows: proof that the Rust side is alive and that the
/// tree-sitter toolchain inside java-atlas-scan works.
#[derive(Debug, Serialize)]
#[serde(rename_all = "camelCase")]
pub struct AppInfo {
    pub version: &'static str,
    pub platform: &'static str,
    pub java_grammar_ok: bool,
}

#[tauri::command]
pub fn app_info() -> AppInfo {
    AppInfo {
        version: env!("CARGO_PKG_VERSION"),
        platform: std::env::consts::OS,
        java_grammar_ok: java_atlas_scan::java_parser().is_ok(),
    }
}

fn join_error(e: impl std::fmt::Display) -> AppError {
    AppError::new(ErrorKind::Internal, e.to_string())
}

fn open(state: &AppState) -> Result<Arc<OpenProject>, AppError> {
    state
        .get()
        .ok_or_else(|| AppError::new(ErrorKind::NoProjectOpen, "no project is open"))
}

#[tauri::command]
pub async fn open_project(
    path: String,
    on_progress: Channel<ScanEvent>,
    app: AppHandle,
    state: State<'_, AppState>,
) -> Result<ProjectSummary, AppError> {
    let scanned = {
        let root = PathBuf::from(&path);
        tauri::async_runtime::spawn_blocking(move || {
            // A closed window drops the channel; a failed send is not our problem.
            project::scan(&root, &|event| {
                let _ = on_progress.send(event);
            })
        })
        .await
        .map_err(join_error)?
    };

    let project = match scanned {
        Ok(project) => project,
        Err(err) => {
            // A recent entry that no longer opens is dead weight.
            if matches!(err.kind, ErrorKind::NotFound | ErrorKind::NotAJavaProject) {
                recent::forget(&app, &path);
            }
            return Err(err);
        }
    };

    let summary = project.summary.clone();
    recent::remember(
        &app,
        RecentProject {
            name: summary.name.clone(),
            path,
            build_system: summary.build_system.clone(),
        },
    );
    state.set(project);
    Ok(summary)
}

#[tauri::command]
pub async fn get_atlas_view(
    request: ViewRequest,
    state: State<'_, AppState>,
) -> Result<AtlasViewDto, AppError> {
    let project = open(&state)?;
    tauri::async_runtime::spawn_blocking(move || project.view(&request))
        .await
        .map_err(join_error)?
}

#[tauri::command]
pub async fn get_node_details(
    key: String,
    state: State<'_, AppState>,
) -> Result<NodeDetailsDto, AppError> {
    let project = open(&state)?;
    tauri::async_runtime::spawn_blocking(move || project.details(&key))
        .await
        .map_err(join_error)?
}

#[tauri::command]
pub async fn get_edge_evidence(
    from: String,
    to: String,
    state: State<'_, AppState>,
) -> Result<Vec<EvidenceDto>, AppError> {
    let project = open(&state)?;
    tauri::async_runtime::spawn_blocking(move || project.evidence(&from, &to))
        .await
        .map_err(join_error)?
}

#[tauri::command]
pub async fn get_tree(state: State<'_, AppState>) -> Result<TreeDto, AppError> {
    let project = open(&state)?;
    tauri::async_runtime::spawn_blocking(move || project.tree())
        .await
        .map_err(join_error)
}

#[tauri::command]
pub async fn search(
    query: String,
    state: State<'_, AppState>,
) -> Result<Vec<SearchHitDto>, AppError> {
    let project = open(&state)?;
    tauri::async_runtime::spawn_blocking(move || project.search(&query))
        .await
        .map_err(join_error)
}

#[tauri::command]
pub fn recent_projects(app: AppHandle) -> Vec<RecentProject> {
    recent::load(&app)
}

#[tauri::command]
pub fn forget_recent_project(path: String, app: AppHandle) -> Vec<RecentProject> {
    recent::forget(&app, &path);
    recent::load(&app)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn app_info_reports_a_working_grammar() {
        let info = app_info();
        assert!(info.java_grammar_ok);
        assert!(!info.version.is_empty());
    }
}
