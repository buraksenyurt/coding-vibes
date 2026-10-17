//! Prints what java-atlas understood from a project.
//!
//!     cargo run -p java-atlas-scan --example inspect -- samples/atlas-bank
//!     cargo run -p java-atlas-scan --example inspect -- samples/atlas-bank --sources
//!
//! Without `--sources` only the build files are read (phase 1). With it every
//! `.java` file is parsed too (phase 2) and a timing line goes to stderr.

use std::path::PathBuf;
use std::process::ExitCode;
use std::time::Instant;

fn main() -> ExitCode {
    let args: Vec<String> = std::env::args().skip(1).collect();
    let with_sources = args.iter().any(|a| a == "--sources");
    let Some(root) = args
        .iter()
        .find(|a| !a.starts_with("--"))
        .map(PathBuf::from)
    else {
        eprintln!("usage: inspect <project-folder> [--sources]");
        return ExitCode::from(2);
    };

    let started = Instant::now();
    let result = if with_sources {
        java_atlas_scan::read_project(&root, &|_| {})
    } else {
        java_atlas_scan::read_build(&root)
    };

    match result {
        Ok(facts) => {
            print!("{}", facts.describe());
            if with_sources {
                println!();
                print!("{}", facts.describe_sources());
                eprintln!(
                    "{} file(s) in {:.0?}",
                    facts.file_count(),
                    started.elapsed()
                );
            }
            ExitCode::SUCCESS
        }
        Err(e) => {
            eprintln!("error: {e}");
            ExitCode::FAILURE
        }
    }
}
