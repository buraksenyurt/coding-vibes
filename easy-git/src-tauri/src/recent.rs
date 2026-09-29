//! "Recently opened" list, persisted with tauri-plugin-store as JSON in the
//! app data folder (%APPDATA%\dev.buraksenyurt.easygit\easy-git.json).

use tauri::{AppHandle, Runtime};
use tauri_plugin_store::StoreExt;

use crate::dto::RecentRepository;

const STORE_FILE: &str = "easy-git.json";
const KEY: &str = "recentRepositories";
const MAX_RECENT: usize = 8;

pub fn load<R: Runtime>(app: &AppHandle<R>) -> Vec<RecentRepository> {
    let Ok(store) = app.store(STORE_FILE) else {
        return Vec::new();
    };
    store
        .get(KEY)
        .and_then(|value| serde_json::from_value(value).ok())
        .unwrap_or_default()
}

pub fn remember<R: Runtime>(app: &AppHandle<R>, entry: RecentRepository) {
    let list = push_front(load(app), entry);
    if let Ok(store) = app.store(STORE_FILE) {
        store.set(KEY, serde_json::to_value(list).unwrap_or_default());
        // Losing the recent list is not worth an error dialog.
        let _ = store.save();
    }
}

/// Most recent first, no duplicates, capped.
fn push_front(mut list: Vec<RecentRepository>, entry: RecentRepository) -> Vec<RecentRepository> {
    list.retain(|r| !r.path.eq_ignore_ascii_case(&entry.path));
    list.insert(0, entry);
    list.truncate(MAX_RECENT);
    list
}

#[cfg(test)]
mod tests {
    use super::*;

    fn repo(path: &str) -> RecentRepository {
        RecentRepository {
            name: path.into(),
            path: path.into(),
        }
    }

    #[test]
    fn moves_reopened_repository_to_the_front() {
        let list = push_front(vec![repo("a"), repo("b")], repo("B"));
        let paths: Vec<_> = list.iter().map(|r| r.path.as_str()).collect();
        assert_eq!(paths, ["B", "a"]);
    }

    #[test]
    fn keeps_at_most_eight() {
        let list = (0..20).fold(Vec::new(), |acc, i| push_front(acc, repo(&i.to_string())));
        assert_eq!(list.len(), MAX_RECENT);
        assert_eq!(list[0].path, "19");
    }
}
