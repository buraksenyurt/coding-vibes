//! Facts → nodes and type-level edges.
//!
//! Two passes, because a name can only be resolved once every name is known:
//!
//! 1. **Declare**: create module, package and type nodes; fill the symbol table
//!    (`fully qualified name → NodeId`).
//! 2. **Resolve**: for every file, turn its imports and name candidates into
//!    edges from the file's main type to whatever they denote.

use std::collections::{BTreeMap, HashMap, HashSet};

use super::{Edge, Evidence, MatchMethod, Node, NodeId, NodeKind, Weight};
use crate::facts::{ProjectFacts, SourceFileFacts};
use crate::intern::{Interner, Sym};

/// `java.lang` is imported implicitly; these are the names worth recognising.
const JAVA_LANG: &[&str] = &[
    "AbstractMethodError",
    "Appendable",
    "ArithmeticException",
    "ArrayIndexOutOfBoundsException",
    "ArrayStoreException",
    "AssertionError",
    "AutoCloseable",
    "Boolean",
    "Byte",
    "CharSequence",
    "Character",
    "Class",
    "ClassCastException",
    "ClassLoader",
    "ClassNotFoundException",
    "CloneNotSupportedException",
    "Cloneable",
    "Comparable",
    "Deprecated",
    "Double",
    "Enum",
    "Error",
    "Exception",
    "Float",
    "FunctionalInterface",
    "IllegalAccessException",
    "IllegalArgumentException",
    "IllegalMonitorStateException",
    "IllegalStateException",
    "IndexOutOfBoundsException",
    "InheritableThreadLocal",
    "InstantiationException",
    "Integer",
    "InterruptedException",
    "Iterable",
    "LinkageError",
    "Long",
    "Math",
    "Module",
    "ModuleLayer",
    "NegativeArraySizeException",
    "NoSuchFieldException",
    "NoSuchMethodException",
    "NullPointerException",
    "Number",
    "NumberFormatException",
    "Object",
    "OutOfMemoryError",
    "Override",
    "Package",
    "Process",
    "ProcessBuilder",
    "ProcessHandle",
    "Readable",
    "Record",
    "ReflectiveOperationException",
    "Runnable",
    "Runtime",
    "RuntimeException",
    "SafeVarargs",
    "SecurityException",
    "Short",
    "StackOverflowError",
    "StackTraceElement",
    "StackWalker",
    "StrictMath",
    "String",
    "StringBuffer",
    "StringBuilder",
    "StringIndexOutOfBoundsException",
    "SuppressWarnings",
    "System",
    "Thread",
    "ThreadGroup",
    "ThreadLocal",
    "Throwable",
    "TypeNotPresentException",
    "UnsupportedOperationException",
    "VirtualMachineError",
    "Void",
];

/// `javax` packages that ship with the JDK. Others (`javax.inject`,
/// `javax.servlet`, `javax.persistence`…) are ordinary libraries.
const JDK_JAVAX: &[&str] = &[
    "javax.accessibility",
    "javax.annotation.processing",
    "javax.crypto",
    "javax.imageio",
    "javax.lang.model",
    "javax.management",
    "javax.naming",
    "javax.net",
    "javax.print",
    "javax.rmi",
    "javax.script",
    "javax.security",
    "javax.smartcardio",
    "javax.sound",
    "javax.sql",
    "javax.swing",
    "javax.tools",
    "javax.transaction.xa",
    "javax.xml",
];

pub(super) fn is_jdk(package: &str) -> bool {
    let under = |root: &str| package == root || package.starts_with(&format!("{root}."));
    ["java", "jdk", "sun", "com.sun.net.httpserver"]
        .iter()
        .any(|r| under(r))
        || JDK_JAVAX.iter().any(|r| under(r))
}

/// The package part of a qualified type name: everything before the first
/// segment that starts with a capital letter (`java.util.Map.Entry` → `java.util`).
pub(super) fn package_of(fqn: &str) -> String {
    let segments: Vec<&str> = fqn.split('.').collect();
    let first_type = segments
        .iter()
        .position(|s| s.starts_with(char::is_uppercase))
        .unwrap_or(segments.len().saturating_sub(1));
    segments[..first_type].join(".")
}

/// Number of leading dot-separated segments two names share.
fn common_segments(a: &str, b: &str) -> usize {
    a.split('.')
        .zip(b.split('.'))
        .take_while(|(x, y)| x == y)
        .count()
}

/// Result of pass 1 + 2, handed to the analysis.
pub(super) struct Resolved {
    pub nodes: Vec<Node>,
    pub type_edges: Vec<Edge>,
    /// `facts.modules[i]` → its module node (`None` for aggregators).
    pub module_nodes: Vec<Option<NodeId>>,
    /// `group:artifact` → library node.
    pub libraries: HashMap<String, NodeId>,
}

/// What a name or import points at.
enum Target {
    Internal(NodeId),
    External(String),
}

struct Resolver<'f> {
    facts: &'f ProjectFacts,
    nodes: Vec<Node>,
    names: Interner,
    /// Fully qualified type name → type node.
    types: HashMap<Sym, NodeId>,
    /// Package names that exist somewhere in the project.
    internal_packages: HashSet<Sym>,
    /// (module index, package) → package node.
    packages: HashMap<(usize, Sym), NodeId>,
    module_nodes: Vec<Option<NodeId>>,
    libraries: HashMap<String, NodeId>,
    /// JDK and unknown external packages, created on first use.
    externals: HashMap<String, NodeId>,
    /// (module index, external package) → where it goes; matching is costly enough to cache.
    external_cache: HashMap<(usize, String), NodeId>,
    edges: BTreeMap<(NodeId, NodeId), Edge>,
}

pub(super) fn resolve(facts: &ProjectFacts) -> Resolved {
    let mut r = Resolver {
        facts,
        nodes: Vec::new(),
        names: Interner::default(),
        types: HashMap::new(),
        internal_packages: HashSet::new(),
        packages: HashMap::new(),
        module_nodes: Vec::new(),
        libraries: HashMap::new(),
        externals: HashMap::new(),
        external_cache: HashMap::new(),
        edges: BTreeMap::new(),
    };
    r.declare();
    for (index, module) in facts.modules.iter().enumerate() {
        for file in &module.files {
            r.resolve_file(index, file);
        }
    }
    Resolved {
        nodes: r.nodes,
        // BTreeMap iteration is ordered by (from, to): stable output for free.
        type_edges: r.edges.into_values().collect(),
        module_nodes: r.module_nodes,
        libraries: r.libraries,
    }
}

impl Resolver<'_> {
    fn add_node(
        &mut self,
        kind: NodeKind,
        name: String,
        fqn: String,
        parent: Option<NodeId>,
        is_test: bool,
    ) -> NodeId {
        let id = NodeId(self.nodes.len() as u32);
        self.nodes.push(Node {
            id,
            kind,
            name,
            fqn,
            parent,
            is_test,
        });
        id
    }

    // -- pass 1 -------------------------------------------------------------

    fn declare(&mut self) {
        let facts = self.facts;
        for module in &facts.modules {
            let node = (!module.aggregator).then(|| {
                let kind = NodeKind::Module {
                    id: module.id.clone(),
                    jpms: module.jpms.clone(),
                };
                self.add_node(kind, module.name.clone(), module.id.clone(), None, false)
            });
            self.module_nodes.push(node);
        }

        for (index, module) in facts.modules.iter().enumerate() {
            let Some(module_node) = self.module_nodes[index] else {
                continue;
            };
            for file in &module.files {
                let package = self.package_node(index, module_node, &file.package);
                if !file.is_test {
                    self.nodes[package.index()].is_test = false;
                }
                for t in &file.types {
                    let fqn = qualify(&file.package, &t.name);
                    let sym = self.names.intern(&fqn);
                    if self.types.contains_key(&sym) {
                        // Same class in two modules (or main and test): first one wins.
                        continue;
                    }
                    let kind = NodeKind::Type {
                        kind: t.kind,
                        lines: t.lines,
                        file: file.path.clone(),
                    };
                    let id = self.add_node(kind, t.name.clone(), fqn, Some(package), file.is_test);
                    self.types.insert(sym, id);
                }
            }
        }

        // Every declared library becomes an island, even if the code never uses it:
        // an unused island is a finding, not something to hide.
        for module in &facts.modules {
            for dep in &module.external_deps {
                let coords = dep.coordinates();
                if self.libraries.contains_key(&coords) {
                    continue;
                }
                let kind = NodeKind::Library {
                    group: dep.group.clone(),
                    artifact: dep.artifact.clone(),
                    version: dep.version.clone(),
                    packages: Vec::new(),
                };
                let id = self.add_node(kind, dep.artifact.clone(), coords.clone(), None, false);
                self.libraries.insert(coords, id);
            }
        }
    }

    fn package_node(&mut self, module_index: usize, module_node: NodeId, package: &str) -> NodeId {
        let sym = self.names.intern(package);
        self.internal_packages.insert(sym);
        if let Some(&id) = self.packages.get(&(module_index, sym)) {
            return id;
        }
        let exported = self.facts.modules[module_index].jpms.as_ref().map(|j| {
            j.exports
                .iter()
                .any(|e| e.package == package && e.to.is_empty())
        });
        let name = if package.is_empty() {
            "(default)".to_owned()
        } else {
            package.to_owned()
        };
        // Starts as test-only; the first main file flips it.
        let id = self.add_node(
            NodeKind::Package { exported },
            name.clone(),
            name,
            Some(module_node),
            true,
        );
        self.packages.insert((module_index, sym), id);
        id
    }

    // -- pass 2 -------------------------------------------------------------

    fn resolve_file(&mut self, module_index: usize, file: &SourceFileFacts) {
        // A file's uses are attributed to its first top-level type. Member
        // types share the file of their outer type, so this loses little.
        let Some(owner) = file
            .types
            .iter()
            .find(|t| !t.name.contains('.'))
            .and_then(|t| self.names.get(&qualify(&file.package, &t.name)))
            .and_then(|sym| self.types.get(&sym).copied())
        else {
            return; // package-info.java, or a duplicate type skipped in pass 1
        };

        // Names declared in this very file never create edges.
        let own: HashSet<&str> = file
            .types
            .iter()
            .flat_map(|t| {
                [
                    t.name.as_str(),
                    t.name.rsplit('.').next().unwrap_or_default(),
                ]
            })
            .collect();

        let mut single: HashMap<&str, String> = HashMap::new();
        let mut on_demand: Vec<&str> = Vec::new();
        let mut static_owners: Vec<String> = Vec::new();

        for import in &file.imports {
            if !is_qualified_name(&import.path) {
                continue;
            }
            let type_fqn = match (import.is_static, import.on_demand) {
                (false, true) => {
                    on_demand.push(&import.path);
                    continue; // `import p.*;` names nothing by itself
                }
                (false, false) => import.path.clone(),
                // import static a.b.C.m;  → the type is a.b.C
                (true, false) => import
                    .path
                    .rsplit_once('.')
                    .map(|(t, _)| t.to_owned())
                    .unwrap_or_default(),
                (true, true) => import.path.clone(),
            };
            if !import.is_static {
                if let Some(simple) = import.path.rsplit('.').next() {
                    single.insert(simple, type_fqn.clone());
                }
            } else {
                static_owners.push(type_fqn.clone());
            }
            if let Some(target) = self.resolve_fqn(&type_fqn) {
                self.add_use(module_index, owner, target, file, import.line, 1);
            }
        }

        for r in &file.references {
            if !is_qualified_name(&r.name) {
                continue;
            }
            let first = r.name.split('.').next().unwrap_or_default();
            if own.contains(r.name.as_str()) || own.contains(first) {
                continue;
            }
            let target =
                self.resolve_name(&r.name, &file.package, &single, &on_demand, &static_owners);
            if let Some(target) = target {
                self.add_use(module_index, owner, target, file, r.line, r.count);
            }
        }
    }

    fn internal_type(&self, fqn: &str) -> Option<NodeId> {
        self.names
            .get(fqn)
            .and_then(|s| self.types.get(&s).copied())
    }

    /// A fully qualified type name → its target.
    fn resolve_fqn(&self, fqn: &str) -> Option<Target> {
        if let Some(id) = self.internal_type(fqn) {
            return Some(Target::Internal(id));
        }
        let package = package_of(fqn);
        if package.is_empty() {
            return None;
        }
        // A project package but an unknown type: generated code, or a typo.
        // Not external, and we cannot say more; skip it.
        if self
            .names
            .get(&package)
            .is_some_and(|s| self.internal_packages.contains(&s))
        {
            return None;
        }
        Some(Target::External(package))
    }

    /// Java's scoping rules for a type name, in order (JLS §6.4.1):
    /// single-type import → same package → on-demand imports → `java.lang`.
    fn resolve_name(
        &self,
        name: &str,
        package: &str,
        single: &HashMap<&str, String>,
        on_demand: &[&str],
        static_owners: &[String],
    ) -> Option<Target> {
        let (first, rest) = match name.split_once('.') {
            Some((f, r)) => (f, Some(r)),
            None => (name, None),
        };
        let with_rest = |base: &str| match rest {
            Some(r) => format!("{base}.{r}"),
            None => base.to_owned(),
        };

        if let Some(fqn) = single.get(first) {
            return self.resolve_fqn(&with_rest(fqn));
        }
        if let Some(id) = self.internal_type(&with_rest(&qualify(package, first))) {
            return Some(Target::Internal(id));
        }
        for p in on_demand {
            if let Some(id) = self.internal_type(&with_rest(&format!("{p}.{first}"))) {
                return Some(Target::Internal(id));
            }
        }
        // `import static a.b.Outer.*;` also brings Outer's member types.
        for owner in static_owners {
            if let Some(id) = self.internal_type(&with_rest(&format!("{owner}.{first}"))) {
                return Some(Target::Internal(id));
            }
        }
        if JAVA_LANG.contains(&first) {
            return Some(Target::External("java.lang".to_owned()));
        }
        // `java.util.concurrent.atomic.AtomicLong` written out in full.
        if rest.is_some() && first.starts_with(char::is_lowercase) {
            return self.resolve_fqn(name);
        }
        // `LOG`, a type parameter we missed, a member type of a super class…
        None
    }

    fn add_use(
        &mut self,
        module_index: usize,
        from: NodeId,
        target: Target,
        file: &SourceFileFacts,
        line: u32,
        count: u32,
    ) {
        let to = match target {
            Target::Internal(id) => id,
            Target::External(package) => self.external_node(module_index, package),
        };
        if from == to {
            return;
        }
        let weight = if file.is_test {
            Weight {
                main: 0,
                test: count,
            }
        } else {
            Weight {
                main: count,
                test: 0,
            }
        };
        let evidence = [Evidence {
            file: file.path.clone(),
            line,
        }];
        self.edges
            .entry((from, to))
            .or_insert_with(|| Edge {
                from,
                to,
                weight: Weight::default(),
                evidence: Vec::new(),
            })
            .merge(weight, &evidence);
    }

    // -- external packages --------------------------------------------------

    fn external_node(&mut self, module_index: usize, package: String) -> NodeId {
        let key = (module_index, package);
        if let Some(&id) = self.external_cache.get(&key) {
            return id;
        }
        let package = &key.1;
        let id = if is_jdk(package) {
            self.plain_external(package, NodeKind::Jdk)
        } else if let Some((library, method)) = self.match_library(module_index, package) {
            if let NodeKind::Library { packages, .. } = &mut self.nodes[library.index()].kind
                && !packages.iter().any(|(p, _)| p == package)
            {
                packages.push((package.clone(), method));
                packages.sort();
            }
            library
        } else {
            self.plain_external(package, NodeKind::Unknown)
        };
        self.external_cache.insert(key, id);
        id
    }

    fn plain_external(&mut self, package: &str, kind: NodeKind) -> NodeId {
        if let Some(&id) = self.externals.get(package) {
            return id;
        }
        let id = self.add_node(kind, package.to_owned(), package.to_owned(), None, false);
        self.externals.insert(package.to_owned(), id);
        id
    }

    /// Which declared library provides `package`? Libraries the module itself
    /// declares are preferred; then any library of the project.
    fn match_library(&self, module_index: usize, package: &str) -> Option<(NodeId, MatchMethod)> {
        let own: Vec<String> = self.facts.modules[module_index]
            .external_deps
            .iter()
            .map(|d| d.coordinates())
            .collect();
        let all: Vec<&String> = self.libraries.keys().collect();
        let prefer_own = |candidates: Vec<&String>| -> Option<String> {
            candidates
                .iter()
                .find(|c| own.contains(c))
                .or_else(|| candidates.iter().min())
                .map(|c| (*c).clone())
        };

        // 1. A jar that really contains the package.
        let in_jar: Vec<&String> = self
            .facts
            .libraries
            .iter()
            .filter(|l| {
                l.packages
                    .binary_search_by(|p| p.as_str().cmp(package))
                    .is_ok()
            })
            .filter_map(|l| {
                self.libraries
                    .get_key_value(&format!("{}:{}", l.group, l.artifact))
                    .map(|(k, _)| k)
            })
            .collect();
        if let Some(coords) = prefer_own(in_jar) {
            return Some((self.libraries[&coords], MatchMethod::Jar));
        }

        // 2. Name similarity between the package and the library's coordinates.
        let best = |candidates: &[&String]| -> Option<String> {
            let mut scored: Vec<(u32, &String)> = candidates
                .iter()
                .map(|c| (name_score(c, package), *c))
                .filter(|(score, _)| *score > 0)
                .collect();
            scored.sort_by(|a, b| b.0.cmp(&a.0).then(a.1.cmp(b.1)));
            let (top_score, top) = *scored.first()?;
            // Everything that ties for first place must come from one group
            // (gwt-dev / gwt-user: same archipelago, pick the first). Ties across
            // groups (two unrelated com.google.* jars) mean: do not guess.
            let tied_groups: HashSet<&str> = scored
                .iter()
                .take_while(|(s, _)| *s == top_score)
                .map(|(_, c)| group(c))
                .collect();
            (tied_groups.len() == 1).then(|| top.clone())
        };
        let own_refs: Vec<&String> = all.iter().copied().filter(|c| own.contains(c)).collect();
        best(&own_refs)
            .or_else(|| best(&all).filter(|c| name_score(c, package) >= STRONG))
            .map(|coords| (self.libraries[&coords], MatchMethod::Name))
    }
}

fn group(coords: &str) -> &str {
    coords.split(':').next().unwrap_or_default()
}

/// Score from which a name match is trusted without competition.
const STRONG: u32 = 40;

/// Artifact words too generic to identify a library.
const GENERIC_WORDS: &[&str] = &[
    "api",
    "core",
    "impl",
    "common",
    "commons",
    "java",
    "annotations",
    "lib",
    "all",
];

/// How plausible is it that `package` comes from the library `group:artifact`?
///
/// | Signal | Example | Points |
/// |---|---|---|
/// | the package lies under the whole groupId | `org.slf4j` ⊂ `org.slf4j` | 100 + 10/segment |
/// | three or more shared segments | `com.fasterxml.jackson.core` ~ `…jackson.databind` | 50 + 10/segment |
/// | only two shared segments | `com.google.guava` ~ `com.google.common` | 20 (weak) |
/// | the package is above the group | `org.junit` vs `org.junit.jupiter` | 0 |
/// | the whole artifact name is a segment | `truth` in `com.google.common.truth` | +40 |
/// | one artifact word is a segment | `text` of `commons-text` | +30 |
pub(super) fn name_score(coords: &str, package: &str) -> u32 {
    let (group, artifact) = coords.split_once(':').unwrap_or((coords, ""));
    let group_len = group.split('.').count();
    let common = common_segments(group, package);

    let package_len = package.split('.').count();

    let mut score = if common == group_len {
        100 + 10 * common as u32
    } else if common == package_len {
        // The package sits *above* the group (`org.junit` vs `org.junit.jupiter`):
        // a library does not put classes in its parent's namespace.
        0
    } else if common >= 3 {
        50 + 10 * common as u32
    } else if common == 2 {
        20
    } else {
        0
    };

    let segments: HashSet<&str> = package.split('.').collect();
    let joined = artifact.replace(['-', '_', '.'], "");
    if segments.contains(joined.as_str()) {
        // The whole artifact name is a package segment: `junit` in `org.junit`.
        score += STRONG;
    } else if artifact
        .split(['-', '_', '.'])
        .any(|w| w.len() >= 3 && !GENERIC_WORDS.contains(&w) && segments.contains(w))
    {
        // One word of it is: `text` of `commons-text` in `org.apache.commons.text`.
        score += 30;
    }
    score
}

/// Rejects "names" that are not Java names: binary junk in a broken file
/// can produce candidates full of spaces and symbols.
fn is_qualified_name(name: &str) -> bool {
    !name.is_empty()
        && name.split('.').all(|part| {
            let mut chars = part.chars();
            chars
                .next()
                .is_some_and(|c| c.is_alphabetic() || c == '_' || c == '$')
                && chars.all(|c| c.is_alphanumeric() || c == '_' || c == '$')
        })
}

/// `com.x` + `Money` → `com.x.Money`; the default package has no prefix.
fn qualify(package: &str, name: &str) -> String {
    if package.is_empty() {
        name.to_owned()
    } else {
        format!("{package}.{name}")
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn package_ends_before_the_first_capital() {
        assert_eq!(package_of("java.util.Map.Entry"), "java.util");
        assert_eq!(package_of("org.slf4j.Logger"), "org.slf4j");
        assert_eq!(package_of("a.b.c"), "a.b");
        assert_eq!(package_of("Lonely"), "");
    }

    #[test]
    fn jdk_packages() {
        assert!(is_jdk("java.util"));
        assert!(is_jdk("java.util.concurrent.atomic"));
        assert!(is_jdk("javax.sql"));
        assert!(!is_jdk("javax.inject"), "javax.inject is a library");
        assert!(!is_jdk("javafx.scene"), "prefix must end at a dot");
    }

    #[test]
    fn name_scores_rank_the_right_library_first() {
        let pick = |package: &str, libs: &[&str]| -> Option<String> {
            let mut scored: Vec<(u32, &str)> =
                libs.iter().map(|l| (name_score(l, package), *l)).collect();
            scored.sort_by_key(|s| std::cmp::Reverse(s.0));
            (scored[0].0 > 0 && scored.get(1).is_none_or(|s| s.0 < scored[0].0))
                .then(|| scored[0].1.to_owned())
        };
        let junit5 = "org.junit.jupiter:junit-jupiter";
        let junit4 = "junit:junit";
        assert_eq!(
            pick("org.junit.jupiter.api", &[junit5, junit4]).as_deref(),
            Some(junit5)
        );
        assert_eq!(
            pick("org.junit", &[junit5, junit4]).as_deref(),
            Some(junit4)
        );
        assert_eq!(
            pick("junit.framework", &[junit5, junit4]).as_deref(),
            Some(junit4)
        );
        assert_eq!(
            pick(
                "org.apache.commons.text",
                &[
                    "org.apache.commons:commons-lang3",
                    "org.apache.commons:commons-text"
                ]
            )
            .as_deref(),
            Some("org.apache.commons:commons-text")
        );
        assert_eq!(
            pick(
                "com.google.common.truth",
                &["com.google.truth:truth", "com.google.guava:guava"]
            )
            .as_deref(),
            Some("com.google.truth:truth")
        );
        // Only "com.google" in common, and two candidates: no answer.
        assert_eq!(
            pick(
                "com.google.common.util.concurrent.internal",
                &["com.google.guava:failureaccess", "com.google.jimfs:jimfs"]
            ),
            None
        );
        assert_eq!(
            name_score("com.google.guava:guava", "com.google.common.base"),
            20,
            "weak on its own"
        );
    }

    #[test]
    fn junk_is_not_a_name() {
        assert!(is_qualified_name("java.util.Map"));
        assert!(is_qualified_name("$Proxy_1"));
        assert!(!is_qualified_name("s  ~  t"));
        assert!(!is_qualified_name("a..b"));
        assert!(!is_qualified_name(""));
    }

    #[test]
    fn shared_segments() {
        assert_eq!(
            common_segments(
                "com.fasterxml.jackson.core",
                "com.fasterxml.jackson.databind"
            ),
            3
        );
        assert_eq!(common_segments("org.slf4j", "org.slf4j"), 2);
        assert_eq!(common_segments("org.junit.jupiter", "org.slf4j"), 1);
    }
}
