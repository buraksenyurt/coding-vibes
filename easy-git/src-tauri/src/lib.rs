//! The Tauri shell: wires commands, state and plugins together.
//! Business logic lives in easy-git-core; git access in easy-git-repo.

mod commands;
mod dto;
mod error;
mod recent;
mod state;

use state::AppState;

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .plugin(tauri_plugin_dialog::init())
        .plugin(tauri_plugin_store::Builder::new().build())
        .manage(AppState::default())
        .invoke_handler(tauri::generate_handler![
            commands::open_repository,
            commands::get_metro_map,
            commands::get_commit_details,
            commands::get_branch_stats,
            commands::recent_repositories,
            commands::forget_recent_repository,
        ])
        .run(tauri::generate_context!())
        .expect("error while running easy-git");
}
