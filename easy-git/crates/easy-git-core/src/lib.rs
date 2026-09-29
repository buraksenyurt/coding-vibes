//! easy-git-core: the domain of easy-git.
//!
//! This crate must stay free of `gix` and `tauri`. It receives history through
//! the [`HistorySource`] trait and turns it into a metro map. That constraint
//! keeps the layout algorithm testable with tiny in-memory graphs.

mod id;
mod model;
mod source;

pub use id::{CommitId, ParseCommitIdError};
pub use model::{Commit, GitRef, Head, RefKind, Signature, Timestamp};
pub use source::{HistorySource, SourceError};

/// Name of the product, shared by every layer.
pub const PRODUCT_NAME: &str = "easy-git";
