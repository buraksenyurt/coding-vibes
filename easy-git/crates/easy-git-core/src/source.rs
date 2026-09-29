use std::fmt;

use crate::{Commit, CommitId, GitRef, Head};

/// Anything that can hand us refs and commits.
///
/// `easy-git-repo` implements it on top of gix; tests implement it with a
/// handful of hand-written commits. The core never learns which one it got.
pub trait HistorySource {
    /// Branches (local and remote-tracking) and tags, peeled to commits.
    fn refs(&self) -> Result<Vec<GitRef>, SourceError>;

    fn head(&self) -> Result<Head, SourceError>;

    /// Commits reachable from `tips`, newest first, at most `limit` of them.
    ///
    /// Parents may point outside the returned window; callers must cope.
    fn walk(&self, tips: &[CommitId], limit: usize) -> Result<Vec<Commit>, SourceError>;
}

/// Errors are written by hand here to keep the core crate dependency free.
/// Compare with `thiserror` in easy-git-repo, which generates the same code.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum SourceError {
    NotARepository(String),
    Corrupt(String),
    Backend(String),
}

impl fmt::Display for SourceError {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        match self {
            SourceError::NotARepository(path) => {
                write!(f, "'{path}' is not inside a git repository")
            }
            SourceError::Corrupt(detail) => write!(f, "the repository looks damaged: {detail}"),
            SourceError::Backend(detail) => write!(f, "could not read the repository: {detail}"),
        }
    }
}

impl std::error::Error for SourceError {}
