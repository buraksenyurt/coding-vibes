//! The Tauri shell: wires commands, state and plugins together.
//! Analysis lives in java-atlas-core; disk and parser access in java-atlas-scan.

mod commands;

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .plugin(tauri_plugin_dialog::init())
        .invoke_handler(tauri::generate_handler![commands::app_info])
        .run(tauri::generate_context!())
        .expect("error while running java-atlas");
}
