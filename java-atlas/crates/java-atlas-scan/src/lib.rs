//! java-atlas-scan — the eyes of java-atlas.
//!
//! Everything that touches the disk or a parser lives here: build files
//! (phase 1, [`build_files`]) and Java sources via tree-sitter (phase 2,
//! [`sources`]). The output is plain data ("facts") that java-atlas-core
//! understands.

pub mod build_files;
mod paths;
pub mod sources;

use std::path::Path;

pub use build_files::read_build;
pub use sources::{Progress, read_sources};

use java_atlas_core::facts::ProjectFacts;

/// Reads a whole project: build files first, then every source file.
pub fn read_project(
    root: &Path,
    on_progress: &(dyn Fn(Progress) + Sync),
) -> Result<ProjectFacts, ScanError> {
    let mut facts = read_build(root)?;
    read_sources(root, &mut facts, on_progress)?;
    Ok(facts)
}

use thiserror::Error;
use tree_sitter::Parser;

/// Errors raised while scanning a project.
///
/// Only problems that make the whole scan meaningless are errors. A single
/// odd dependency line becomes a [`java_atlas_core::facts::ScanWarning`].
#[derive(Debug, Error)]
pub enum ScanError {
    /// The folder has no build file and no `.java` file.
    #[error(
        "'{path}' does not look like a Java project (no pom.xml, no Gradle build, no .java files)"
    )]
    NotAJavaProject { path: String },
    /// A file could not be read.
    #[error("cannot read '{path}': {source}")]
    Io {
        path: String,
        #[source]
        source: std::io::Error,
    },
    /// A `pom.xml` is not well-formed XML.
    #[error("'{path}' is not a valid POM: {message}")]
    Xml { path: String, message: String },
    /// `gradle/libs.versions.toml` is not valid TOML.
    #[error("'{path}' is not a valid version catalog: {message}")]
    Toml { path: String, message: String },
    /// The tree-sitter runtime refused the Java grammar (version mismatch).
    #[error("the Java grammar could not be loaded: {0}")]
    Grammar(#[from] tree_sitter::LanguageError),
}

/// Creates a parser already wired to the Java grammar.
pub fn java_parser() -> Result<Parser, ScanError> {
    let mut parser = Parser::new();
    parser.set_language(&tree_sitter_java::LANGUAGE.into())?;
    Ok(parser)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn the_java_grammar_loads() {
        assert!(java_parser().is_ok());
    }
}
