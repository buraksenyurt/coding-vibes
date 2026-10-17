//! Prints what java-atlas understood from a project's build files.
//!
//!     cargo run -p java-atlas-scan --example inspect -- samples/atlas-bank

use std::path::PathBuf;
use std::process::ExitCode;

fn main() -> ExitCode {
    let Some(root) = std::env::args_os().nth(1).map(PathBuf::from) else {
        eprintln!("usage: inspect <project-folder>");
        return ExitCode::from(2);
    };
    match java_atlas_scan::read_build(&root) {
        Ok(facts) => {
            print!("{}", facts.describe());
            ExitCode::SUCCESS
        }
        Err(e) => {
            eprintln!("error: {e}");
            ExitCode::FAILURE
        }
    }
}
