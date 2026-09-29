use std::sync::{Arc, Mutex};

use easy_git_core::MetroMap;
use easy_git_repo::GixSource;

/// The repository currently on screen, and the last map built from it.
///
/// The `Mutex`es guard only swaps of `Arc`s; the long work (walking history)
/// happens on a clone of the `Arc` after the lock is released, so a slow
/// repository never blocks another command.
#[derive(Default)]
pub struct AppState {
    source: Mutex<Option<Arc<GixSource>>>,
    /// Commit details and branch stats are answered from this cache instead
    /// of walking the repository again.
    map: Mutex<Option<Arc<MetroMap>>>,
}

impl AppState {
    pub fn set_source(&self, source: GixSource) -> Arc<GixSource> {
        let source = Arc::new(source);
        *self.source.lock().expect("state lock poisoned") = Some(Arc::clone(&source));
        *self.map.lock().expect("state lock poisoned") = None;
        source
    }

    pub fn source(&self) -> Option<Arc<GixSource>> {
        self.source.lock().expect("state lock poisoned").clone()
    }

    pub fn set_map(&self, map: MetroMap) -> Arc<MetroMap> {
        let map = Arc::new(map);
        *self.map.lock().expect("state lock poisoned") = Some(Arc::clone(&map));
        map
    }

    pub fn map(&self) -> Option<Arc<MetroMap>> {
        self.map.lock().expect("state lock poisoned").clone()
    }
}
