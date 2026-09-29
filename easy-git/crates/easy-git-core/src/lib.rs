//! easy-git-core: the domain of easy-git.
//!
//! This crate must stay free of `gix` and `tauri`. It receives history through
//! the [`HistorySource`] trait and turns it into a [`MetroMap`]. That
//! constraint keeps the layout algorithm testable with tiny in-memory graphs.

mod describe;
mod id;
mod infer;
mod metro;
mod model;
mod priority;
mod source;
mod stats;

#[cfg(test)]
mod testing;

pub use describe::describe;
pub use id::{CommitId, ParseCommitIdError};
pub use infer::branch_from_merge_message;
pub use metro::{
    Column, Lane, LaneId, LaneLabel, MapOptions, MetroMap, PALETTE_SIZE, Transition,
    TransitionKind, build_metro_map, layout,
};
pub use model::{Commit, GitRef, Head, RefKind, Signature, Timestamp};
pub use priority::BranchPriority;
pub use source::{HistorySource, SourceError};
pub use stats::{
    BranchStats, DEFAULT_STALE_DAYS, Upstream, base_branch, branch_stats, branches_containing,
    reachable,
};

/// Name of the product, shared by every layer.
pub const PRODUCT_NAME: &str = "easy-git";
