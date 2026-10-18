//! The Tauri shell: wires commands, state and plugins together.
//! Analysis lives in java-atlas-core; disk and parser access in java-atlas-scan.

mod commands;
mod dto;
mod error;
mod project;
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
            commands::app_info,
            commands::open_project,
            commands::get_atlas_view,
            commands::get_node_details,
            commands::get_edge_evidence,
            commands::get_tree,
            commands::search,
            commands::recent_projects,
            commands::forget_recent_project,
        ])
        .run(tauri::generate_context!())
        .expect("error while running java-atlas");
}
