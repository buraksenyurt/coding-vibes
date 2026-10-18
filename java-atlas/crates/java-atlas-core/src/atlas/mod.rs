//! The atlas: a resolved dependency graph built from [`ProjectFacts`].
//!
//! Facts say what the scanner *saw* ("`AccountService.java` mentions
//! `AuditLog`"). The atlas says what it *means* ("type
//! `com.atlasbank.service.account.AccountService` depends on type
//! `com.atlasbank.service.audit.AuditLog`, 3 times, first on line 5").
//!
//! The graph is an **arena**: every node lives in one `Vec<Node>` and is
//! referred to by its index ([`NodeId`]). Edges hold two indices. No `Rc`,
//! no `RefCell`, no lifetimes — the borrow checker has nothing to argue about.
//!
//! Building happens in `resolve` (facts → nodes and type-level edges) and
//! `analysis` (aggregation, cycles, JPMS, declarations, metrics).

mod analysis;
mod describe;
mod layout;
mod resolve;
mod search;
mod view;

pub use layout::{Archipelago, LAND, Layout};
pub use view::{View, ViewEdge, ViewFilters};

use crate::facts::{JpmsFacts, ProjectFacts, TypeKind};

/// Index of a node in [`Atlas::nodes`].
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, PartialOrd, Ord)]
pub struct NodeId(pub u32);

impl NodeId {
    pub fn index(self) -> usize {
        self.0 as usize
    }
}

/// How an external package was tied to a declared library.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, PartialOrd, Ord)]
pub enum MatchMethod {
    /// The package was found inside the library's jar (local Maven/Gradle cache).
    Jar,
    /// The package name resembles the library's coordinates (see `name_score`).
    Name,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum NodeKind {
    /// A build module: a region on the map.
    Module {
        /// The build id (`com.atlasbank:bank-api`, `:shop-core`).
        id: String,
        jpms: Option<JpmsFacts>,
    },
    /// A package of one module: a city.
    Package {
        /// `None` when the module has no `module-info.java`.
        exported: Option<bool>,
    },
    /// A type: a building.
    Type {
        kind: TypeKind,
        lines: u32,
        file: String,
    },
    /// A declared external library: an island.
    Library {
        group: String,
        artifact: String,
        version: Option<String>,
        /// Packages seen in the code that were attributed to this library.
        packages: Vec<(String, MatchMethod)>,
    },
    /// A JDK package (`java.util`, `javax.sql`…): the mainland.
    Jdk,
    /// An external package no declared library explains.
    Unknown,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Node {
    pub id: NodeId,
    pub kind: NodeKind,
    /// Short label: type name, package name, artifactId…
    pub name: String,
    /// Full name: `com.atlasbank.common.Money`, `org.slf4j:slf4j-api`…
    pub fqn: String,
    /// Type → package → module. `None` for modules and external nodes.
    pub parent: Option<NodeId>,
    /// Only test code lives here.
    pub is_test: bool,
}

impl Node {
    /// Internal = part of the project (module, package or type).
    pub fn is_internal(&self) -> bool {
        matches!(
            self.kind,
            NodeKind::Module { .. } | NodeKind::Package { .. } | NodeKind::Type { .. }
        )
    }
}

/// How many uses an edge stands for, split by main and test code.
#[derive(Debug, Clone, Copy, Default, PartialEq, Eq)]
pub struct Weight {
    pub main: u32,
    pub test: u32,
}

impl Weight {
    pub fn total(self) -> u32 {
        self.main + self.test
    }

    fn add(&mut self, other: Weight) {
        self.main += other.main;
        self.test += other.test;
    }
}

/// Where a dependency was seen: the answer to "why is there a road here?".
#[derive(Debug, Clone, PartialEq, Eq, PartialOrd, Ord)]
pub struct Evidence {
    pub file: String,
    pub line: u32,
}

/// Evidence kept per edge; enough to explain, small enough to ship to the UI.
pub const MAX_EVIDENCE: usize = 20;

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Edge {
    pub from: NodeId,
    pub to: NodeId,
    pub weight: Weight,
    pub evidence: Vec<Evidence>,
}

impl Edge {
    fn merge(&mut self, weight: Weight, evidence: &[Evidence]) {
        self.weight.add(weight);
        for e in evidence {
            if self.evidence.len() >= MAX_EVIDENCE {
                break;
            }
            if !self.evidence.contains(e) {
                self.evidence.push(e.clone());
            }
        }
    }
}

/// Zoom level of an edge list (see PLAN 3.1).
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Level {
    Type,
    Package,
    Module,
}

/// Packages that depend on each other in a circle (a strongly connected component).
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Cycle {
    /// Package nodes, sorted by name.
    pub packages: Vec<NodeId>,
    /// The lightest edge inside the cycle: the cheapest one to remove.
    pub weakest: (NodeId, NodeId, u32),
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum ViolationKind {
    /// The target package is not exported by its JPMS module (to the user).
    NotExported,
    /// Both modules are JPMS modules, but the user does not `requires` the owner.
    NotRead,
}

/// A road that crosses a customs border without a gate.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Violation {
    pub kind: ViolationKind,
    pub from_module: NodeId,
    pub to_package: NodeId,
    pub evidence: Vec<Evidence>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum FindingKind {
    /// Declared in the build file, never used in code.
    UnusedModuleDependency,
    UnusedLibrary,
    /// Used in code, not declared. `reachable`: it still compiles because the
    /// dependency arrives transitively through a declared one.
    UndeclaredModuleDependency {
        reachable: bool,
    },
    UndeclaredLibrary,
}

/// A mismatch between the build file and the code.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Finding {
    pub kind: FindingKind,
    pub module: NodeId,
    pub target: NodeId,
}

/// Robert C. Martin's coupling metrics (also computed by JDepend).
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct Metrics {
    /// Afferent coupling: how many other packages depend on this one.
    pub ca: u32,
    /// Efferent coupling: how many packages this one depends on (JDK excluded).
    pub ce: u32,
}

impl Metrics {
    /// I = Ce / (Ca + Ce): 0 = stable (used a lot, depends on little), 1 = unstable.
    pub fn instability(self) -> Option<f64> {
        let total = self.ca + self.ce;
        (total > 0).then(|| f64::from(self.ce) / f64::from(total))
    }
}

/// The resolved, analysed project.
#[derive(Debug, Clone)]
pub struct Atlas {
    pub name: String,
    pub nodes: Vec<Node>,
    pub type_edges: Vec<Edge>,
    pub package_edges: Vec<Edge>,
    pub module_edges: Vec<Edge>,
    pub cycles: Vec<Cycle>,
    pub violations: Vec<Violation>,
    pub findings: Vec<Finding>,
    /// Indexed by node; `Some` for packages and modules.
    pub metrics: Vec<Option<Metrics>>,
}

impl Atlas {
    /// Resolves and analyses the facts of a project.
    pub fn build(facts: &ProjectFacts) -> Atlas {
        let resolved = resolve::resolve(facts);
        analysis::analyse(facts, resolved)
    }

    pub fn node(&self, id: NodeId) -> &Node {
        &self.nodes[id.index()]
    }

    pub fn edges(&self, level: Level) -> &[Edge] {
        match level {
            Level::Type => &self.type_edges,
            Level::Package => &self.package_edges,
            Level::Module => &self.module_edges,
        }
    }

    /// The module a node belongs to (itself for a module).
    pub fn module_of(&self, id: NodeId) -> Option<NodeId> {
        let mut current = id;
        loop {
            let node = self.node(current);
            if matches!(node.kind, NodeKind::Module { .. }) {
                return Some(current);
            }
            current = node.parent?;
        }
    }

    /// Finds a node by its full name, e.g. a package `com.atlasbank.common`.
    /// Package names can repeat across modules; the first match is returned.
    pub fn find(&self, fqn: &str) -> Option<NodeId> {
        self.nodes.iter().find(|n| n.fqn == fqn).map(|n| n.id)
    }

    pub fn metrics(&self, id: NodeId) -> Option<Metrics> {
        self.metrics[id.index()]
    }
}

#[cfg(test)]
mod tests {
    //! The resolution rules, checked on hand-built facts: no disk, no parser.

    use super::*;
    use crate::facts::{
        BuildSystem, ExternalDep, ImportFacts, LibraryIndex, ModuleFacts, NameRef, ProjectFacts,
        Scope, SourceFileFacts, TypeFacts, TypeKind, VersionSource,
    };

    fn ty(name: &str) -> TypeFacts {
        TypeFacts {
            name: name.into(),
            kind: TypeKind::Class,
            is_public: true,
            line: 1,
            lines: 1,
        }
    }

    fn import(path: &str, is_static: bool, on_demand: bool) -> ImportFacts {
        ImportFacts {
            path: path.into(),
            is_static,
            on_demand,
            line: 1,
        }
    }

    fn uses(names: &[&str]) -> Vec<NameRef> {
        names
            .iter()
            .map(|n| NameRef {
                name: (*n).into(),
                line: 2,
                count: 1,
            })
            .collect()
    }

    fn file(
        package: &str,
        types: &[&str],
        imports: Vec<ImportFacts>,
        refs: &[&str],
    ) -> SourceFileFacts {
        SourceFileFacts {
            path: format!(
                "{}/{}.java",
                package.replace('.', "/"),
                types.first().copied().unwrap_or("package-info")
            ),
            package: package.into(),
            is_test: false,
            lines: 10,
            imports,
            types: types.iter().map(|t| ty(t)).collect(),
            references: uses(refs),
            has_errors: false,
        }
    }

    fn library(group: &str, artifact: &str) -> ExternalDep {
        ExternalDep {
            group: group.into(),
            artifact: artifact.into(),
            version: Some("1".into()),
            version_source: VersionSource::Explicit,
            scope: Scope::Compile,
            inherited: false,
        }
    }

    fn project(
        files: Vec<SourceFileFacts>,
        deps: Vec<ExternalDep>,
        libraries: Vec<LibraryIndex>,
    ) -> ProjectFacts {
        ProjectFacts {
            name: "t".into(),
            build_system: BuildSystem::Plain,
            modules: vec![ModuleFacts {
                id: "app".into(),
                name: "app".into(),
                path: String::new(),
                build_file: String::new(),
                aggregator: false,
                source_roots: Vec::new(),
                jpms: None,
                files,
                module_deps: Vec::new(),
                external_deps: deps,
            }],
            warnings: Vec::new(),
            libraries,
        }
    }

    /// Targets of the type `from`, as full names.
    fn targets(atlas: &Atlas, from: &str) -> Vec<String> {
        let from = atlas.find(from).unwrap();
        let mut t: Vec<String> = atlas
            .type_edges
            .iter()
            .filter(|e| e.from == from)
            .map(|e| atlas.node(e.to).fqn.clone())
            .collect();
        t.sort();
        t
    }

    #[test]
    fn javas_scoping_rules_in_order() {
        let atlas = Atlas::build(&project(
            vec![
                file("a", &["Money", "List"], vec![], &[]),
                file("b", &["Money"], vec![], &[]),
                file("c", &["Helper"], vec![], &[]),
                file(
                    "app",
                    &["Main"],
                    vec![import("b.Money", false, false), import("c", false, true)],
                    // Money: single import beats nothing else here; Helper: via c.*;
                    // String: java.lang; LOG and T: dropped.
                    &["Money", "Helper", "String", "LOG", "T"],
                ),
                // Same package wins over java.lang: a.List, not java.lang (there is none anyway).
                file("a", &["User"], vec![], &["Money", "List"]),
            ],
            vec![],
            vec![],
        ));
        assert_eq!(
            targets(&atlas, "app.Main"),
            ["b.Money", "c.Helper", "java.lang"]
        );
        assert_eq!(targets(&atlas, "a.User"), ["a.List", "a.Money"]);
    }

    #[test]
    fn member_types_static_imports_and_full_names() {
        let atlas = Atlas::build(&project(
            vec![
                file("m", &["Account", "Account.Status"], vec![], &["Status"]),
                file("u", &["Util"], vec![], &[]),
                file(
                    "app",
                    &["Main"],
                    vec![
                        import("m.Account.Status", false, false),
                        import("u.Util.helper", true, false),
                    ],
                    &["java.util.concurrent.atomic.AtomicLong", "Status"],
                ),
            ],
            vec![],
            vec![],
        ));
        assert_eq!(
            targets(&atlas, "app.Main"),
            ["java.util.concurrent.atomic", "m.Account.Status", "u.Util"]
        );
        // A type's own member types are not dependencies.
        assert!(targets(&atlas, "m.Account").is_empty());
    }

    #[test]
    fn a_jar_beats_a_name_guess() {
        // By name, `org.slf4j` belongs to slf4j-api. The jar index says the
        // project's odd "logging-bundle" library ships it: the jar wins.
        let files = vec![file(
            "app",
            &["Main"],
            vec![import("org.slf4j.Logger", false, false)],
            &[],
        )];
        let deps = vec![
            library("org.slf4j", "slf4j-api"),
            library("com.example", "logging-bundle"),
        ];
        let index = vec![LibraryIndex {
            group: "com.example".into(),
            artifact: "logging-bundle".into(),
            version: "1".into(),
            jar: "x.jar".into(),
            packages: vec!["org.slf4j".into()],
        }];

        let by_name = Atlas::build(&project(files.clone(), deps.clone(), vec![]));
        assert_eq!(targets(&by_name, "app.Main"), ["org.slf4j:slf4j-api"]);

        let by_jar = Atlas::build(&project(files, deps, index));
        assert_eq!(targets(&by_jar, "app.Main"), ["com.example:logging-bundle"]);
        let NodeKind::Library { packages, .. } = &by_jar
            .node(by_jar.find("com.example:logging-bundle").unwrap())
            .kind
        else {
            panic!("not a library")
        };
        assert_eq!(packages, &[("org.slf4j".to_owned(), MatchMethod::Jar)]);
    }

    #[test]
    fn unexplained_external_packages_are_unknown() {
        let atlas = Atlas::build(&project(
            vec![file(
                "app",
                &["Main"],
                vec![import("org.mystery.Thing", false, false)],
                &[],
            )],
            vec![library("org.slf4j", "slf4j-api")],
            vec![],
        ));
        let to = atlas.find("org.mystery").unwrap();
        assert_eq!(atlas.node(to).kind, NodeKind::Unknown);
    }
}
