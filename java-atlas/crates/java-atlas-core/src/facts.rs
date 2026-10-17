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
    /// Source folders declared in the build file, relative to the module folder.
    /// Empty means the convention: `src/main/java` and `src/test/java`.
    pub source_roots: Vec<SourceRoot>,
    pub module_deps: Vec<ModuleDep>,
    pub external_deps: Vec<ExternalDep>,
    /// Present when the module has a `module-info.java` (phase 2).
    pub jpms: Option<JpmsFacts>,
    /// Every `.java` file under the module's source roots (phase 2).
    pub files: Vec<SourceFileFacts>,
}

// ---------------------------------------------------------------------------
// Source facts (phase 2)
// ---------------------------------------------------------------------------

/// The flavour of a type declaration; decides the building's shape on the map.
#[derive(Debug, Clone, Copy, PartialEq, Eq, PartialOrd, Ord)]
pub enum TypeKind {
    Class,
    Interface,
    Enum,
    Record,
    Annotation,
}

impl fmt::Display for TypeKind {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        f.write_str(match self {
            TypeKind::Class => "class",
            TypeKind::Interface => "interface",
            TypeKind::Enum => "enum",
            TypeKind::Record => "record",
            TypeKind::Annotation => "@interface",
        })
    }
}

/// A type declared in a file. Member types are included with a dotted name.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct TypeFacts {
    /// Simple name; member types are `Outer.Inner`.
    pub name: String,
    pub kind: TypeKind,
    pub is_public: bool,
    /// First line of the declaration (1-based).
    pub line: u32,
    /// Number of lines the declaration spans; sizes the building.
    pub lines: u32,
}

/// One `import` line, as written.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ImportFacts {
    /// Without `import`, `static`, `.*` and `;`: `com.x.Money`, `com.x.Money.zero`, `com.x`.
    pub path: String,
    pub is_static: bool,
    /// `import com.x.*;` / `import static com.x.Y.*;`
    pub on_demand: bool,
    pub line: u32,
}

/// A name in the code that *may* denote a type. Resolved (or discarded) in
/// phase 3 against imports, the package and the symbol table.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct NameRef {
    /// `Account`, `Account.Status` or `java.util.concurrent.atomic.AtomicLong`.
    pub name: String,
    /// First line where it appears.
    pub line: u32,
    /// How many times it appears in the file.
    pub count: u32,
}

/// Everything read from one `.java` file.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct SourceFileFacts {
    /// Relative to the project root, `/` separated.
    pub path: String,
    /// Declared package; `""` for the default package.
    pub package: String,
    /// Lives under a test source root.
    pub is_test: bool,
    pub lines: u32,
    pub imports: Vec<ImportFacts>,
    pub types: Vec<TypeFacts>,
    /// Sorted by name, one entry per distinct name.
    pub references: Vec<NameRef>,
    /// tree-sitter found syntax errors; the facts may be incomplete.
    pub has_errors: bool,
}

/// `requires [transitive] [static] x.y;`
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Requires {
    pub module: String,
    pub transitive: bool,
    pub is_static: bool,
}

/// `exports p [to a, b];` and `opens p [to a, b];`
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct PackageGrant {
    pub package: String,
    /// Empty = to everyone.
    pub to: Vec<String>,
}

/// `provides Service with Impl1, Impl2;`
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Provides {
    pub service: String,
    pub with: Vec<String>,
}

/// The content of a `module-info.java`.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct JpmsFacts {
    /// The JPMS module name, e.g. `com.atlasbank.domain`.
    pub name: String,
    /// `open module …`: every package is open for reflection.
    pub open: bool,
    pub file: String,
    pub requires: Vec<Requires>,
    pub exports: Vec<PackageGrant>,
    pub opens: Vec<PackageGrant>,
    pub uses: Vec<String>,
    pub provides: Vec<Provides>,
}

/// A folder holding `.java` files.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct SourceRoot {
    /// Relative to the module folder, `/` separated.
    pub path: String,
    pub is_test: bool,
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

    /// Number of source files over all modules.
    pub fn file_count(&self) -> usize {
        self.modules.iter().map(|m| m.files.len()).sum()
    }

    /// A plain-text dump of the *source* facts (phase 2), module by module.
    /// Like [`ProjectFacts::describe`], it backs snapshot tests.
    pub fn describe_sources(&self) -> String {
        let mut out = String::new();
        for m in self.modules.iter().filter(|m| !m.aggregator) {
            let _ = writeln!(out, "{} · {} file(s)", m.id, m.files.len());

            if let Some(j) = &m.jpms {
                let open = if j.open { "open " } else { "" };
                let _ = writeln!(out, "  {open}module {}  <{}>", j.name, j.file);
                for r in &j.requires {
                    let t = if r.transitive { "transitive " } else { "" };
                    let st = if r.is_static { "static " } else { "" };
                    let _ = writeln!(out, "    requires {t}{st}{}", r.module);
                }
                for (word, grants) in [("exports", &j.exports), ("opens", &j.opens)] {
                    for g in grants {
                        let to = if g.to.is_empty() {
                            String::new()
                        } else {
                            format!(" to {}", g.to.join(", "))
                        };
                        let _ = writeln!(out, "    {word} {}{to}", g.package);
                    }
                }
                for u in &j.uses {
                    let _ = writeln!(out, "    uses {u}");
                }
                for p in &j.provides {
                    let _ = writeln!(out, "    provides {} with {}", p.service, p.with.join(", "));
                }
            }

            for f in &m.files {
                let test = if f.is_test { " [test]" } else { "" };
                let broken = if f.has_errors { " [syntax errors]" } else { "" };
                let package = if f.package.is_empty() {
                    "(default)"
                } else {
                    f.package.as_str()
                };
                let _ = writeln!(
                    out,
                    "  {}{test}{broken}\n    package {package} · {} lines",
                    f.path, f.lines
                );
                for t in &f.types {
                    let vis = if t.is_public { "public " } else { "" };
                    let _ = writeln!(
                        out,
                        "    {vis}{} {}  L{} +{}",
                        t.kind, t.name, t.line, t.lines
                    );
                }
                for i in &f.imports {
                    let st = if i.is_static { "static " } else { "" };
                    let star = if i.on_demand { ".*" } else { "" };
                    let _ = writeln!(out, "    import {st}{}{star}  L{}", i.path, i.line);
                }
                if !f.references.is_empty() {
                    let refs: Vec<String> = f
                        .references
                        .iter()
                        .map(|r| {
                            if r.count > 1 {
                                format!("{}×{}", r.name, r.count)
                            } else {
                                r.name.clone()
                            }
                        })
                        .collect();
                    let _ = writeln!(out, "    uses {}", refs.join(" "));
                }
            }
            let _ = writeln!(out);
        }
        out
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
                    source_roots: Vec::new(),
                    jpms: None,
                    files: Vec::new(),
                    module_deps: vec![],
                    external_deps: vec![],
                },
                ModuleFacts {
                    id: ":app".into(),
                    name: "app".into(),
                    path: "app".into(),
                    build_file: "app/build.gradle.kts".into(),
                    aggregator: false,
                    source_roots: Vec::new(),
                    jpms: None,
                    files: Vec::new(),
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
