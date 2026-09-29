//! easy-git-core: the domain of easy-git.
//!
//! This crate must stay free of `gix` and `tauri`. It receives history through
//! a trait and turns it into a metro map. That constraint keeps the layout
//! algorithm testable with tiny in-memory graphs.

/// Name of the product, shared by every layer.
pub const PRODUCT_NAME: &str = "easy-git";
