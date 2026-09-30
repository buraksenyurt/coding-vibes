use serde::Serialize;
use ts_rs::TS;

/// What the front end receives when a command fails. Tauri serialises the
/// `Err` side of a command's `Result`, so this type must be `Serialize`.
#[derive(Debug, Clone, Serialize, TS, thiserror::Error)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
#[error("{message}")]
pub struct AppError {
    pub kind: ErrorKind,
    pub message: String,
}

#[derive(Debug, Clone, Copy, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub enum ErrorKind {
    /// The folder does not exist any more (moved, renamed or deleted).
    NotFound,
    NotARepository,
    NoRepositoryOpen,
    ReadFailed,
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

impl From<easy_git_repo::RepoError> for AppError {
    fn from(err: easy_git_repo::RepoError) -> Self {
        let kind = match err {
            easy_git_repo::RepoError::NotARepository(_) => ErrorKind::NotARepository,
            _ => ErrorKind::ReadFailed,
        };
        Self::new(kind, err.to_string())
    }
}

impl From<easy_git_core::SourceError> for AppError {
    fn from(err: easy_git_core::SourceError) -> Self {
        let kind = match err {
            easy_git_core::SourceError::NotARepository(_) => ErrorKind::NotARepository,
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
