//! `#[tauri::command]` functions: the only doors from the WebView into Rust.
//!
//! Every command that touches git moves the work to a worker thread with
//! `spawn_blocking`, so the window stays responsive on large repositories.

use std::path::PathBuf;
use std::sync::Arc;
use std::time::{SystemTime, UNIX_EPOCH};

use easy_git_core::{
    DEFAULT_STALE_DAYS, HistorySource, MapOptions, MetroMap, branch_stats, build_metro_map,
};
use easy_git_repo::GixSource;
use tauri::{AppHandle, State};

use crate::dto::{
    BranchStatsDto, CommitDetailsDto, HeadDto, MetroMapDto, RecentRepository, RepoSummary,
};
use crate::error::{AppError, ErrorKind};
use crate::recent;
use crate::state::AppState;

fn join_error(e: impl std::fmt::Display) -> AppError {
    AppError::new(ErrorKind::Internal, e.to_string())
}

fn cached_map(state: &AppState) -> Result<Arc<MetroMap>, AppError> {
    state
        .map()
        .ok_or_else(|| AppError::new(ErrorKind::NoRepositoryOpen, "no repository is open"))
}

#[tauri::command]
pub async fn open_repository(
    path: String,
    app: AppHandle,
    state: State<'_, AppState>,
) -> Result<RepoSummary, AppError> {
    let opened = {
        let path = path.clone();
        tauri::async_runtime::spawn_blocking(move || open_checked(&path))
            .await
            .map_err(join_error)?
    };

    let source = match opened {
        Ok(source) => source,
        Err(err) => {
            // A recent entry that no longer opens is dead weight: drop it so
            // the menu stops offering it. The front end tells the user.
            if matches!(err.kind, ErrorKind::NotFound | ErrorKind::NotARepository) {
                recent::forget(&app, &path);
            }
            return Err(err);
        }
    };

    let head = source.head()?;
    let summary = RepoSummary {
        name: source.name(),
        path: source.root().display().to_string(),
        head: HeadDto::from(&head),
    };
    state.set_source(source);
    recent::remember(
        &app,
        RecentRepository {
            name: summary.name.clone(),
            path: summary.path.clone(),
        },
    );
    Ok(summary)
}

/// Tells "the folder is gone" apart from "the folder is not a repository".
fn open_checked(path: &str) -> Result<GixSource, AppError> {
    let folder = PathBuf::from(path);
    if !folder.is_dir() {
        return Err(AppError::new(
            ErrorKind::NotFound,
            format!("'{path}' could not be found"),
        ));
    }
    Ok(GixSource::open(folder)?)
}

#[tauri::command]
pub async fn get_metro_map(
    limit: Option<usize>,
    state: State<'_, AppState>,
) -> Result<MetroMapDto, AppError> {
    let source = state
        .source()
        .ok_or_else(|| AppError::new(ErrorKind::NoRepositoryOpen, "no repository is open"))?;
    let options = MapOptions {
        limit: limit.unwrap_or(MapOptions::default().limit),
        ..MapOptions::default()
    };

    let map =
        tauri::async_runtime::spawn_blocking(move || build_metro_map(source.as_ref(), &options))
            .await
            .map_err(join_error)??;
    let map = state.set_map(map);
    Ok(MetroMapDto::from(map.as_ref()))
}

#[tauri::command]
pub fn get_commit_details(
    id: String,
    state: State<'_, AppState>,
) -> Result<CommitDetailsDto, AppError> {
    let map = cached_map(&state)?;
    let column = id
        .parse()
        .ok()
        .and_then(|id| map.column_of(&id))
        .ok_or_else(|| {
            AppError::new(
                ErrorKind::ReadFailed,
                format!("commit {id} is not on the map"),
            )
        })?;
    Ok(CommitDetailsDto::new(&map, column))
}

#[tauri::command]
pub fn get_branch_stats(state: State<'_, AppState>) -> Result<Vec<BranchStatsDto>, AppError> {
    let map = cached_map(&state)?;
    let now = SystemTime::now()
        .duration_since(UNIX_EPOCH)
        .map(|d| d.as_secs() as i64)
        .unwrap_or(0);
    Ok(branch_stats(&map, now, DEFAULT_STALE_DAYS)
        .iter()
        .map(BranchStatsDto::from)
        .collect())
}

#[tauri::command]
pub fn recent_repositories(app: AppHandle) -> Vec<RecentRepository> {
    recent::load(&app)
}
