use std::sync::{Arc, Mutex};

use crate::project::OpenProject;

/// The project currently on screen.
///
/// The `Mutex` only guards swapping an `Arc`; commands clone the `Arc` and
/// release the lock before doing any work, so a slow view never blocks the
/// next command.
#[derive(Default)]
pub struct AppState {
    project: Mutex<Option<Arc<OpenProject>>>,
}

impl AppState {
    pub fn set(&self, project: OpenProject) -> Arc<OpenProject> {
        let project = Arc::new(project);
        *self.project.lock().expect("state lock poisoned") = Some(Arc::clone(&project));
        project
    }

    pub fn get(&self) -> Option<Arc<OpenProject>> {
        self.project.lock().expect("state lock poisoned").clone()
    }
}
