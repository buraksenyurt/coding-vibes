//! Facts: what the scanner *saw*, before any interpretation.
//!
//! The scanner fills these plain structs; the core later resolves them into
//! a dependency graph. Keeping this boundary means the analysis can be tested
//! with hand-built facts and never needs a real project on disk.
//!
//! Phase 1 covers build files. Phase 2 adds source files and JPMS facts.

use std::fmt::{self, Write as _};

/// Which build tool describes the project.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum BuildSystem {
    Maven,
    Gradle,
    /// No build file; just a folder of `.java` files.
    Plain,
}

impl fmt::Display for BuildSystem {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.write_str(match self {
            BuildSystem::Maven => "Maven",
            BuildSystem::Gradle => "Gradle",
            BuildSystem::Plain => "Plain",
        })
    }
}

/// When a dependency is needed. Maven and Gradle words are mapped onto this.
#[derive(Debug, Clone, Copy, PartialEq, Eq, PartialOrd, Ord)]
pub enum Scope {
    /// Maven `compile`; Gradle `implementation`, `api`.
    Compile,
    /// Maven `provided`; Gradle `compileOnly`, `annotationProcessor`.
    Provided,
    /// Maven `runtime`; Gradle `runtimeOnly`.
    Runtime,
    /// Maven `test`; Gradle `test*`.
    Test,
}

impl fmt::Display for Scope {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.write_str(match self {
            Scope::Compile => "compile",
            Scope::Provided => "provided",
            Scope::Runtime => "runtime",
            Scope::Test => "test",
        })
    }
}

/// Where a dependency's version came from. Shown in the details panel,
/// because "why is this version used?" is a common question.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum VersionSource {
    /// Written literally next to the dependency.
    Explicit,
    /// Came from a `${property}` (Maven).
    Property(String),
    /// Omitted here, supplied by a `dependencyManagement` section (Maven).
    Managed,
    /// Came from a version catalog alias (Gradle `libs.versions.toml`).
    Catalog(String),
    /// Could not be determined (outside parent POM, dynamic Gradle code…).
    Unresolved,
}

/// A library from outside the project, as declared in a build file.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ExternalDep {
    pub group: String,
    pub artifact: String,
    pub version: Option<String>,
    pub version_source: VersionSource,
    pub scope: Scope,
    /// True when the declaration sits in a parent POM, not in the module itself.
    pub inherited: bool,
}

impl ExternalDep {
    /// `group:artifact` — the identity of a library regardless of version.
    pub fn coordinates(&self) -> String {
        format!("{}:{}", self.group, self.artifact)
    }
}

/// A dependency on another module of the same project.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ModuleDep {
    /// The `id` of the target [`ModuleFacts`].
    pub target: String,
    pub scope: Scope,
    /// Gradle `api(...)`: the dependency is part of this module's public API
    /// and leaks to its consumers. Maven has no equivalent; always false there.
    pub exposed: bool,
}

/// One build module: a Maven module or a Gradle (sub)project.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ModuleFacts {
    /// Stable identity. Maven: `group:artifact`; Gradle: project path (`:shop-core`).
    pub id: String,
    /// Short, human friendly name (artifactId or project name).
    pub name: String,
    /// Folder relative to the project root, with `/` separators ("" for the root).
    pub path: String,
    /// The build file this module was read from, relative to the root.
    pub build_file: String,
    /// Only groups other modules (Maven `pom` packaging, Gradle root without sources).
    pub aggregator: bool,
    pub module_deps: Vec<ModuleDep>,
    pub external_deps: Vec<ExternalDep>,
}

/// Something the scanner could not read fully. Never fatal: the atlas is drawn
/// with what was understood and these are shown in a notice bar.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ScanWarning {
    /// File the warning is about, relative to the project root.
    pub file: String,
    pub message: String,
}

/// Everything the scanner learned about one project.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ProjectFacts {
    /// Display name: Maven root artifactId, Gradle `rootProject.name` or folder name.
    pub name: String,
    pub build_system: BuildSystem,
    /// Modules in a stable order (declaration order, root first).
    pub modules: Vec<ModuleFacts>,
    pub warnings: Vec<ScanWarning>,
}

impl ProjectFacts {
    /// Looks a module up by its id.
    pub fn module(&self, id: &str) -> Option<&ModuleFacts> {
        self.modules.iter().find(|m| m.id == id)
    }

    /// A plain-text tree of the facts. Used by the `inspect` example and by
    /// snapshot tests, so its format should change only on purpose.
    pub fn describe(&self) -> String {
        let mut out = String::new();
        let code_modules = self.modules.iter().filter(|m| !m.aggregator).count();
        // `write!` into a String cannot fail; the results are ignored on purpose.
        let _ = writeln!(
            out,
            "{} · {} · {} module(s)",
            self.name, self.build_system, code_modules
        );

        for m in &self.modules {
            let path = if m.path.is_empty() {
                "."
            } else {
                m.path.as_str()
            };
            let tag = if m.aggregator { " (aggregator)" } else { "" };
            let _ = writeln!(out, "\n{}{}  [{}]  <{}>", m.id, tag, path, m.build_file);

            for d in &m.module_deps {
                let exposed = if d.exposed { " api" } else { "" };
                let _ = writeln!(out, "  → {}  {}{}", d.target, d.scope, exposed);
            }
            for d in &m.external_deps {
                let version = d.version.as_deref().unwrap_or("?");
                let source = match &d.version_source {
                    VersionSource::Explicit => String::new(),
                    VersionSource::Property(p) => format!(" (${{{p}}})"),
                    VersionSource::Managed => " (managed)".to_owned(),
                    VersionSource::Catalog(a) => format!(" (libs.{a})"),
                    VersionSource::Unresolved => " (unresolved)".to_owned(),
                };
                let inherited = if d.inherited { " inherited" } else { "" };
                let _ = writeln!(
                    out,
                    "  ⇢ {}:{}  {}{}{}",
                    d.coordinates(),
                    version,
                    d.scope,
                    source,
                    inherited
                );
            }
        }

        if !self.warnings.is_empty() {
            let _ = writeln!(out, "\nwarnings:");
            for w in &self.warnings {
                let _ = writeln!(out, "  ! {}: {}", w.file, w.message);
            }
        }
        out
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    fn tiny() -> ProjectFacts {
        ProjectFacts {
            name: "tiny".into(),
            build_system: BuildSystem::Gradle,
            modules: vec![
                ModuleFacts {
                    id: ":".into(),
                    name: "tiny".into(),
                    path: String::new(),
                    build_file: "build.gradle.kts".into(),
                    aggregator: true,
                    module_deps: vec![],
                    external_deps: vec![],
                },
                ModuleFacts {
                    id: ":app".into(),
                    name: "app".into(),
                    path: "app".into(),
                    build_file: "app/build.gradle.kts".into(),
                    aggregator: false,
                    module_deps: vec![],
                    external_deps: vec![ExternalDep {
                        group: "org.slf4j".into(),
                        artifact: "slf4j-api".into(),
                        version: Some("2.0.16".into()),
                        version_source: VersionSource::Catalog("slf4j.api".into()),
                        scope: Scope::Compile,
                        inherited: false,
                    }],
                },
            ],
            warnings: vec![],
        }
    }

    #[test]
    fn modules_are_found_by_id() {
        let facts = tiny();
        assert_eq!(facts.module(":app").map(|m| m.name.as_str()), Some("app"));
        assert!(facts.module(":missing").is_none());
    }

    #[test]
    fn describe_counts_only_code_modules() {
        let text = tiny().describe();
        assert!(text.starts_with("tiny · Gradle · 1 module(s)"));
        assert!(text.contains("⇢ org.slf4j:slf4j-api:2.0.16  compile (libs.slf4j.api)"));
    }
}
