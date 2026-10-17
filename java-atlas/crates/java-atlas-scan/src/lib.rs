//! java-atlas-scan — the eyes of java-atlas.
//!
//! Everything that touches the disk or a parser lives here: build files
//! (phase 1) and Java sources via tree-sitter (phase 2). The output is plain
//! data ("facts") that java-atlas-core understands.
//!
//! Phase 0 only proves that the tree-sitter toolchain works: the Java grammar
//! is a C library compiled by a build script, so a missing C compiler shows up
//! here first, not three phases later.

use thiserror::Error;
use tree_sitter::Parser;

/// Errors raised while scanning a project.
#[derive(Debug, Error)]
pub enum ScanError {
    /// The tree-sitter runtime refused the Java grammar (version mismatch).
    #[error("the Java grammar could not be loaded: {0}")]
    Grammar(#[from] tree_sitter::LanguageError),
    /// tree-sitter returned no tree at all (cancelled or timed out).
    #[error("the parser returned no syntax tree")]
    NoTree,
}

/// Creates a parser already wired to the Java grammar.
pub fn java_parser() -> Result<Parser, ScanError> {
    let mut parser = Parser::new();
    parser.set_language(&tree_sitter_java::LANGUAGE.into())?;
    Ok(parser)
}

/// Returns the package declared in `source`, if any.
///
/// A deliberately tiny first contact with the syntax tree; phase 2 replaces it
/// with a full file walker.
pub fn declared_package(source: &str) -> Result<Option<String>, ScanError> {
    let mut parser = java_parser()?;
    let tree = parser.parse(source, None).ok_or(ScanError::NoTree)?;
    let root = tree.root_node();

    let mut cursor = root.walk();
    let package = root
        .children(&mut cursor)
        .find(|node| node.kind() == "package_declaration")
        .and_then(|decl| {
            // `package com.example.app;` → children: "package", scoped_identifier, ";"
            let mut inner = decl.walk();
            decl.named_children(&mut inner)
                .find(|n| matches!(n.kind(), "scoped_identifier" | "identifier"))
                .and_then(|name| name.utf8_text(source.as_bytes()).ok())
                .map(str::to_owned)
        });
    Ok(package)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn the_java_grammar_loads() {
        assert!(java_parser().is_ok());
    }

    #[test]
    fn reads_a_qualified_package_name() {
        let src = "package com.atlasbank.service;\n\npublic class Teller {}\n";
        assert_eq!(
            declared_package(src).unwrap().as_deref(),
            Some("com.atlasbank.service")
        );
    }

    #[test]
    fn reads_a_single_segment_package_name() {
        assert_eq!(
            declared_package("package app; class A {}").unwrap().as_deref(),
            Some("app")
        );
    }

    #[test]
    fn the_default_package_has_no_name() {
        assert_eq!(declared_package("class Lonely {}").unwrap(), None);
    }

    #[test]
    fn a_broken_file_still_yields_its_package() {
        // tree-sitter is error tolerant: half-written code still has a tree.
        let src = "package com.atlasbank.api;\npublic class Broken { void f( {";
        assert_eq!(
            declared_package(src).unwrap().as_deref(),
            Some("com.atlasbank.api")
        );
    }
}
