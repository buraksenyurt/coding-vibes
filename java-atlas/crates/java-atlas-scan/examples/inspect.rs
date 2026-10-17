//! Prints what java-atlas understood from a project.
//!
//!     cargo run -p java-atlas-scan --example inspect -- samples/atlas-bank
//!     cargo run -p java-atlas-scan --example inspect -- samples/atlas-bank --sources
//!     cargo run -p java-atlas-scan --example inspect -- samples/atlas-bank --atlas
//!
//! Without flags only the build files are read (phase 1). `--sources` parses
//! every `.java` file too (phase 2); `--atlas` resolves and analyses the result
//! (phase 3). A timing line goes to stderr.

use std::path::PathBuf;
use std::process::ExitCode;
use std::time::Instant;

fn main() -> ExitCode {
    let args: Vec<String> = std::env::args().skip(1).collect();
    let with_atlas = args.iter().any(|a| a == "--atlas");
    let with_sources = with_atlas || args.iter().any(|a| a == "--sources");
    let Some(root) = args
        .iter()
        .find(|a| !a.starts_with("--"))
        .map(PathBuf::from)
    else {
        eprintln!("usage: inspect <project-folder> [--sources | --atlas] [--no-cache]");
        return ExitCode::from(2);
    };

    // Look into ~/.m2 and the Gradle cache unless told not to.
    let options = if args.iter().any(|a| a == "--no-cache") {
        java_atlas_scan::ScanOptions::default()
    } else {
        java_atlas_scan::ScanOptions::with_default_caches()
    };
    let started = Instant::now();
    let result = if with_sources {
        java_atlas_scan::read_project(&root, &options, &|_| {})
    } else {
        java_atlas_scan::read_build(&root)
    };

    match result {
        Ok(facts) if with_atlas => {
            let scanned = started.elapsed();
            let atlas = java_atlas_core::atlas::Atlas::build(&facts);
            print!("{}", atlas.describe());
            eprintln!(
                "{} file(s): scan {:.0?}, analysis {:.0?}",
                facts.file_count(),
                scanned,
                started.elapsed() - scanned
            );
            ExitCode::SUCCESS
        }
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
