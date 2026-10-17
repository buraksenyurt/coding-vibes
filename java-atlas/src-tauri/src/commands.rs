//! Commands the web view may call. Phase 0 has a single health check.

use serde::Serialize;

/// What the front end shows in its status bar: proof that the Rust side is
/// alive and that the tree-sitter toolchain inside java-atlas-scan works.
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
