//! Prints the metro map of any local repository as text — the same drawing
//! the snapshot tests use. Handy to check the lane algorithm without the UI.
//!
//! cargo run -p easy-git-repo --example describe-repo -- <repository folder>

use easy_git_core::{MapOptions, build_metro_map, describe};
use easy_git_repo::GixSource;

fn main() {
    let path = std::env::args().nth(1).unwrap_or_else(|| ".".into());
    let result = GixSource::open(&path)
        .map_err(|e| e.to_string())
        .and_then(|source| build_metro_map(&source, &MapOptions::default()).map_err(|e| e.to_string()));
    match result {
        Ok(map) => print!("{}", describe(&map)),
        Err(err) => {
            eprintln!("{err}");
            std::process::exit(1);
        }
    }
}
