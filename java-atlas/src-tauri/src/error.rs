//! The error the front end receives when a command fails. Tauri serialises
//! the `Err` side of a command's `Result`, so this type must be `Serialize`.

use serde::Serialize;
use ts_rs::TS;

#[derive(Debug, Clone, Serialize, TS, thiserror::Error)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
#[error("{message}")]
pub struct AppError {
    pub kind: ErrorKind,
    pub message: String,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub enum ErrorKind {
    /// The folder does not exist any more (moved, renamed or deleted).
    NotFound,
    /// No build file and no `.java` file.
    NotAJavaProject,
    /// A build file or source could not be read or is malformed.
    ReadFailed,
    /// A view or detail was asked for before a project was opened.
    NoProjectOpen,
    /// A key that does not belong to the open project (stale UI state).
    UnknownNode,
    Internal,
}

impl AppError {
    pub fn new(kind: ErrorKind, message: impl Into<String>) -> Self {
        Self {
            kind,
            message: message.into(),
        }
    }
}

impl From<java_atlas_scan::ScanError> for AppError {
    fn from(err: java_atlas_scan::ScanError) -> Self {
        let kind = match err {
            java_atlas_scan::ScanError::NotAJavaProject { .. } => ErrorKind::NotAJavaProject,
            _ => ErrorKind::ReadFailed,
        };
        Self::new(kind, err.to_string())
    }
}

impl From<tauri::Error> for AppError {
    fn from(err: tauri::Error) -> Self {
        Self::new(ErrorKind::Internal, err.to_string())
    }
}
