//! `#[tauri::command]` functions: the only doors from the WebView into Rust.
//!
//! Every command is async and moves blocking git work to a worker thread with
//! `spawn_blocking`, so the window stays responsive on large repositories.

use std::path::PathBuf;

use easy_git_core::{HistorySource, MapOptions, build_metro_map};
use easy_git_repo::GixSource;
use tauri::{AppHandle, State};

use crate::dto::{HeadDto, MetroMapDto, RecentRepository, RepoSummary};
use crate::error::{AppError, ErrorKind};
use crate::recent;
use crate::state::AppState;

#[tauri::command]
pub async fn open_repository(
    path: String,
    app: AppHandle,
    state: State<'_, AppState>,
) -> Result<RepoSummary, AppError> {
    let source = tauri::async_runtime::spawn_blocking(move || GixSource::open(PathBuf::from(path)))
        .await
        .map_err(|e| AppError::new(ErrorKind::Internal, e.to_string()))??;

    let head = source.head()?;
    let summary = RepoSummary {
        name: source.name(),
        path: source.root().display().to_string(),
        head: HeadDto::from(&head),
    };
    state.set(source);
    recent::remember(
        &app,
        RecentRepository {
            name: summary.name.clone(),
            path: summary.path.clone(),
        },
    );
    Ok(summary)
}

#[tauri::command]
pub async fn get_metro_map(
    limit: Option<usize>,
    state: State<'_, AppState>,
) -> Result<MetroMapDto, AppError> {
    let source = state
        .get()
        .ok_or_else(|| AppError::new(ErrorKind::NoRepositoryOpen, "no repository is open"))?;
    let options = MapOptions {
        limit: limit.unwrap_or(MapOptions::default().limit),
        ..MapOptions::default()
    };

    tauri::async_runtime::spawn_blocking(move || {
        let map = build_metro_map(source.as_ref(), &options)?;
        Ok(MetroMapDto::from(&map))
    })
    .await
    .map_err(|e| AppError::new(ErrorKind::Internal, e.to_string()))?
}

#[tauri::command]
pub fn recent_repositories(app: AppHandle) -> Vec<RecentRepository> {
    recent::load(&app)
}
