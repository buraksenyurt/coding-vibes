//! Build files → [`ProjectFacts`] (modules and their declared dependencies).
//!
//! [`read_build`] looks at the root folder, decides which build tool owns it
//! and hands over to [`maven`] or [`gradle`].

pub mod gradle;
pub mod maven;

use std::path::Path;

use java_atlas_core::facts::{BuildSystem, ModuleFacts, ProjectFacts};

use crate::ScanError;

/// Which build tool a folder belongs to, judged by the files at its root.
pub fn detect(root: &Path) -> Option<BuildSystem> {
    if root.join("pom.xml").is_file() {
        return Some(BuildSystem::Maven);
    }
    let gradle_files = [
        "settings.gradle.kts",
        "settings.gradle",
        "build.gradle.kts",
        "build.gradle",
    ];
    if gradle_files.iter().any(|f| root.join(f).is_file()) {
        return Some(BuildSystem::Gradle);
    }
    has_java_files(root).then_some(BuildSystem::Plain)
}

/// True when at least one `.java` file exists below `root`.
/// `ignore` honours `.gitignore`, so `target/` and `build/` are usually skipped.
fn has_java_files(root: &Path) -> bool {
    ignore::WalkBuilder::new(root)
        .build()
        .filter_map(Result::ok)
        .any(|entry| entry.path().extension().is_some_and(|ext| ext == "java"))
}

/// Reads the build description of the project at `root`.
pub fn read_build(root: &Path) -> Result<ProjectFacts, ScanError> {
    match detect(root) {
        Some(BuildSystem::Maven) => maven::read(root),
        Some(BuildSystem::Gradle) => gradle::read(root),
        Some(BuildSystem::Plain) => Ok(plain(root)),
        None => Err(ScanError::NotAJavaProject {
            path: root.display().to_string(),
        }),
    }
}

/// A folder of sources without a build tool: one module, no declared dependencies.
fn plain(root: &Path) -> ProjectFacts {
    let name = root
        .file_name()
        .map(|n| n.to_string_lossy().into_owned())
        .unwrap_or_else(|| "project".to_owned());
    ProjectFacts {
        name: name.clone(),
        build_system: BuildSystem::Plain,
        modules: vec![ModuleFacts {
            id: name.clone(),
            name,
            path: String::new(),
            build_file: String::new(),
            aggregator: false,
            source_roots: Vec::new(),
            jpms: None,
            files: Vec::new(),
            module_deps: Vec::new(),
            external_deps: Vec::new(),
        }],
        warnings: Vec::new(),
        libraries: Vec::new(),
    }
}
