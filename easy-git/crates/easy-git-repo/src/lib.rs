//! easy-git-repo: the only crate that talks to git through `gix`.
//!
//! Everything gix-specific stays behind [`GixSource`], which implements the
//! core's `HistorySource` trait. If gix changes its API, only this crate moves.

mod convert;
mod error;
mod source;

pub mod fixture;

pub use error::RepoError;
pub use source::GixSource;
