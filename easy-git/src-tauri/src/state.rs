use std::sync::{Arc, Mutex};

use easy_git_repo::GixSource;

/// The repository currently on screen. One at a time.
///
/// The `Mutex` guards only the swap of the `Arc`; the long work (walking
/// history) happens on a clone of the `Arc` after the lock is released, so a
/// slow repository never blocks another command.
#[derive(Default)]
pub struct AppState {
    current: Mutex<Option<Arc<GixSource>>>,
}

impl AppState {
    pub fn set(&self, source: GixSource) -> Arc<GixSource> {
        let source = Arc::new(source);
        *self.current.lock().expect("state lock poisoned") = Some(Arc::clone(&source));
        source
    }

    pub fn get(&self) -> Option<Arc<GixSource>> {
        self.current.lock().expect("state lock poisoned").clone()
    }
}
