use easy_git_core::SourceError;

/// gix has one error type per operation. We fold them into a few cases the UI
/// can explain. `thiserror` writes the `Display` and `Error` impls for us.
#[derive(Debug, thiserror::Error)]
pub enum RepoError {
    #[error("'{0}' is not inside a git repository")]
    NotARepository(String),

    #[error("could not read references: {0}")]
    References(String),

    #[error("could not walk history: {0}")]
    Walk(String),

    #[error("could not decode object {id}: {detail}")]
    Decode { id: String, detail: String },
}

impl From<RepoError> for SourceError {
    fn from(err: RepoError) -> Self {
        match err {
            RepoError::NotARepository(path) => SourceError::NotARepository(path),
            RepoError::Decode { .. } => SourceError::Corrupt(err.to_string()),
            other => SourceError::Backend(other.to_string()),
        }
    }
}
