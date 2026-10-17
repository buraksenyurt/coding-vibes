//! "Recently opened" list, persisted with tauri-plugin-store as JSON in the
//! app data folder (e.g. %APPDATA%\dev.buraksenyurt.javaatlas\java-atlas.json).

use tauri::{AppHandle, Runtime};
use tauri_plugin_store::StoreExt;

use crate::dto::RecentProject;

const STORE_FILE: &str = "java-atlas.json";
const KEY: &str = "recentProjects";
const MAX_RECENT: usize = 8;

pub fn load<R: Runtime>(app: &AppHandle<R>) -> Vec<RecentProject> {
    let Ok(store) = app.store(STORE_FILE) else {
        return Vec::new();
    };
    store
        .get(KEY)
        .and_then(|value| serde_json::from_value(value).ok())
        .unwrap_or_default()
}

pub fn remember<R: Runtime>(app: &AppHandle<R>, entry: RecentProject) {
    save(app, push_front(load(app), entry));
}

/// Drops `path` from the list. Returns whether it was there.
pub fn forget<R: Runtime>(app: &AppHandle<R>, path: &str) -> bool {
    let before = load(app);
    let after = without(before.clone(), path);
    let removed = after.len() != before.len();
    if removed {
        save(app, after);
    }
    removed
}

fn save<R: Runtime>(app: &AppHandle<R>, list: Vec<RecentProject>) {
    if let Ok(store) = app.store(STORE_FILE) {
        store.set(KEY, serde_json::to_value(list).unwrap_or_default());
        // Losing the recent list is not worth an error dialog.
        let _ = store.save();
    }
}

/// Windows paths are case-insensitive; on Linux and macOS they usually are not.
fn same_path(a: &str, b: &str) -> bool {
    if cfg!(windows) {
        a.eq_ignore_ascii_case(b)
    } else {
        a == b
    }
}

fn without(mut list: Vec<RecentProject>, path: &str) -> Vec<RecentProject> {
    list.retain(|r| !same_path(&r.path, path));
    list
}

/// Most recent first, no duplicates, capped.
fn push_front(list: Vec<RecentProject>, entry: RecentProject) -> Vec<RecentProject> {
    let mut list = without(list, &entry.path);
    list.insert(0, entry);
    list.truncate(MAX_RECENT);
    list
}

#[cfg(test)]
mod tests {
    use super::*;

    fn project(path: &str) -> RecentProject {
        RecentProject {
            name: path.into(),
            path: path.into(),
            build_system: "Maven".into(),
        }
    }

    #[test]
    fn moves_a_reopened_project_to_the_front() {
        let list = push_front(vec![project("a"), project("b")], project("b"));
        let paths: Vec<_> = list.iter().map(|r| r.path.as_str()).collect();
        assert_eq!(paths, ["b", "a"]);
    }

    #[test]
    fn keeps_at_most_eight() {
        let list = (0..20).fold(Vec::new(), |acc, i| {
            push_front(acc, project(&i.to_string()))
        });
        assert_eq!(list.len(), MAX_RECENT);
        assert_eq!(list[0].path, "19");
    }
}
