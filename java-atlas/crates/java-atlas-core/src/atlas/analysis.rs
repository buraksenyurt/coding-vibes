//! Everything computed on top of the type-level graph:
//! aggregation, cycles, JPMS rules, build-file findings and metrics.

use std::collections::{BTreeMap, BTreeSet, HashMap, HashSet, VecDeque};

use super::resolve::Resolved;
use super::{
    Atlas, Cycle, Edge, Evidence, Finding, FindingKind, Metrics, Node, NodeId, NodeKind, Violation,
    ViolationKind, Weight,
};
use crate::facts::{BuildSystem, JpmsFacts, ProjectFacts, Scope};

pub(super) fn analyse(facts: &ProjectFacts, resolved: Resolved) -> Atlas {
    let Resolved {
        nodes,
        type_edges,
        module_nodes,
        libraries,
    } = resolved;

    let package_edges = aggregate(&nodes, &type_edges, 1);
    let module_edges = aggregate(&nodes, &type_edges, 2);
    let cycles = cycles(&nodes, &package_edges);
    let violations = jpms_violations(&nodes, &type_edges);
    let findings = findings(facts, &module_nodes, &libraries, &module_edges);
    let metrics = metrics(&nodes, &package_edges, &module_edges);

    Atlas {
        name: facts.name.clone(),
        nodes,
        type_edges,
        package_edges,
        module_edges,
        cycles,
        violations,
        findings,
        metrics,
    }
}

// ---------------------------------------------------------------------------
// Aggregation: type → package → module
// ---------------------------------------------------------------------------

/// Moves an internal node `steps` levels up its parent chain; external nodes
/// (libraries, JDK, unknown) have no parents and stay where they are.
fn lift(nodes: &[Node], mut id: NodeId, steps: usize) -> NodeId {
    for _ in 0..steps {
        match nodes[id.index()].parent {
            Some(parent) => id = parent,
            None => break,
        }
    }
    id
}

fn aggregate(nodes: &[Node], type_edges: &[Edge], steps: usize) -> Vec<Edge> {
    let mut merged: BTreeMap<(NodeId, NodeId), Edge> = BTreeMap::new();
    for e in type_edges {
        let from = lift(nodes, e.from, steps);
        let to = if nodes[e.to.index()].is_internal() {
            lift(nodes, e.to, steps)
        } else {
            e.to
        };
        if from == to {
            continue; // inside the same package / module: not a road between cities
        }
        merged
            .entry((from, to))
            .or_insert_with(|| Edge {
                from,
                to,
                weight: Weight::default(),
                evidence: Vec::new(),
            })
            .merge(e.weight, &e.evidence);
    }
    merged.into_values().collect()
}

// ---------------------------------------------------------------------------
// Cycles: Tarjan's strongly connected components
// ---------------------------------------------------------------------------

/// Tarjan's algorithm on a graph given as adjacency lists over `0..n`.
/// Returns every component, each as a list of vertices.
///
/// One depth-first search; each vertex gets an `index` (visit order) and a
/// `low` link (smallest index reachable through its subtree). A vertex whose
/// `low == index` is the root of a component: everything above it on the
/// stack belongs to that component.
pub(super) fn tarjan(adjacency: &[Vec<usize>]) -> Vec<Vec<usize>> {
    struct State<'a> {
        adjacency: &'a [Vec<usize>],
        index: Vec<Option<usize>>,
        low: Vec<usize>,
        on_stack: Vec<bool>,
        stack: Vec<usize>,
        next: usize,
        components: Vec<Vec<usize>>,
    }

    fn visit(s: &mut State, v: usize) {
        s.index[v] = Some(s.next);
        s.low[v] = s.next;
        s.next += 1;
        s.stack.push(v);
        s.on_stack[v] = true;

        for &w in &s.adjacency[v] {
            match s.index[w] {
                None => {
                    visit(s, w);
                    s.low[v] = s.low[v].min(s.low[w]);
                }
                Some(w_index) if s.on_stack[w] => s.low[v] = s.low[v].min(w_index),
                Some(_) => {} // already in a finished component
            }
        }

        if Some(s.low[v]) == s.index[v] {
            let mut component = Vec::new();
            while let Some(w) = s.stack.pop() {
                s.on_stack[w] = false;
                component.push(w);
                if w == v {
                    break;
                }
            }
            s.components.push(component);
        }
    }

    let n = adjacency.len();
    let mut s = State {
        adjacency,
        index: vec![None; n],
        low: vec![0; n],
        on_stack: vec![false; n],
        stack: Vec::new(),
        next: 0,
        components: Vec::new(),
    };
    for v in 0..n {
        if s.index[v].is_none() {
            visit(&mut s, v);
        }
    }
    s.components
}

fn cycles(nodes: &[Node], package_edges: &[Edge]) -> Vec<Cycle> {
    // Only main code: a test that pokes at two packages is not an architecture problem.
    let main_edges: Vec<&Edge> = package_edges
        .iter()
        .filter(|e| e.weight.main > 0)
        .filter(|e| matches!(nodes[e.to.index()].kind, NodeKind::Package { .. }))
        .collect();

    // Packages ↔ dense indices 0..n for the algorithm.
    let packages: Vec<NodeId> = nodes
        .iter()
        .filter(|n| matches!(n.kind, NodeKind::Package { .. }))
        .map(|n| n.id)
        .collect();
    let dense: HashMap<NodeId, usize> = packages
        .iter()
        .enumerate()
        .map(|(i, id)| (*id, i))
        .collect();
    let mut adjacency = vec![Vec::new(); packages.len()];
    for e in &main_edges {
        adjacency[dense[&e.from]].push(dense[&e.to]);
    }

    let mut cycles: Vec<Cycle> = tarjan(&adjacency)
        .into_iter()
        .filter(|c| c.len() > 1)
        .map(|component| {
            let members: HashSet<NodeId> = component.iter().map(|&i| packages[i]).collect();
            let weakest = main_edges
                .iter()
                .filter(|e| members.contains(&e.from) && members.contains(&e.to))
                .min_by(|a, b| {
                    a.weight
                        .main
                        .cmp(&b.weight.main)
                        .then_with(|| nodes[a.from.index()].fqn.cmp(&nodes[b.from.index()].fqn))
                })
                .map(|e| (e.from, e.to, e.weight.main))
                .expect("a component with two or more members has an inner edge");
            let mut packages: Vec<NodeId> = members.into_iter().collect();
            packages.sort_by(|a, b| nodes[a.index()].fqn.cmp(&nodes[b.index()].fqn));
            Cycle { packages, weakest }
        })
        .collect();
    cycles.sort_by(|a, b| {
        nodes[a.packages[0].index()]
            .fqn
            .cmp(&nodes[b.packages[0].index()].fqn)
    });
    cycles
}

// ---------------------------------------------------------------------------
// JPMS: exports and readability
// ---------------------------------------------------------------------------

fn jpms_of(nodes: &[Node], module: NodeId) -> Option<&JpmsFacts> {
    match &nodes[module.index()].kind {
        NodeKind::Module { jpms, .. } => jpms.as_ref(),
        _ => None,
    }
}

/// Can module `from` read module `to`? Directly via `requires`, or through a
/// chain of `requires transitive` ("implied readability").
fn reads(nodes: &[Node], from: &JpmsFacts, to: &str) -> bool {
    let by_name: HashMap<&str, &JpmsFacts> = nodes
        .iter()
        .filter_map(|n| match &n.kind {
            NodeKind::Module { jpms: Some(j), .. } => Some((j.name.as_str(), j)),
            _ => None,
        })
        .collect();

    let mut seen: HashSet<&str> = HashSet::new();
    let mut queue: VecDeque<&str> = from.requires.iter().map(|r| r.module.as_str()).collect();
    while let Some(name) = queue.pop_front() {
        if name == to {
            return true;
        }
        if !seen.insert(name) {
            continue;
        }
        if let Some(j) = by_name.get(name) {
            queue.extend(
                j.requires
                    .iter()
                    .filter(|r| r.transitive)
                    .map(|r| r.module.as_str()),
            );
        }
    }
    false
}

fn jpms_violations(nodes: &[Node], type_edges: &[Edge]) -> Vec<Violation> {
    let mut found: BTreeMap<(NodeId, NodeId, u8), Vec<Evidence>> = BTreeMap::new();

    for e in type_edges {
        if !matches!(nodes[e.to.index()].kind, NodeKind::Type { .. }) {
            continue;
        }
        let to_package = lift(nodes, e.to, 1);
        let from_module = lift(nodes, e.from, 2);
        let to_module = lift(nodes, e.to, 2);
        if from_module == to_module {
            continue;
        }
        let Some(owner) = jpms_of(nodes, to_module) else {
            continue;
        };
        let user = jpms_of(nodes, from_module);
        let package = &nodes[to_package.index()].fqn;

        let exported = owner.exports.iter().any(|x| {
            &x.package == package
                && (x.to.is_empty() || user.is_some_and(|u| x.to.contains(&u.name)))
        });
        if !exported {
            found
                .entry((from_module, to_package, 0))
                .or_default()
                .extend(e.evidence.iter().cloned());
        } else if let Some(user) = user
            && !reads(nodes, user, &owner.name)
        {
            found
                .entry((from_module, to_package, 1))
                .or_default()
                .extend(e.evidence.iter().cloned());
        }
    }

    found
        .into_iter()
        .map(|((from_module, to_package, kind), mut evidence)| {
            evidence.sort();
            evidence.dedup();
            Violation {
                kind: if kind == 0 {
                    ViolationKind::NotExported
                } else {
                    ViolationKind::NotRead
                },
                from_module,
                to_package,
                evidence,
            }
        })
        .collect()
}

// ---------------------------------------------------------------------------
// Build file vs code
// ---------------------------------------------------------------------------

fn findings(
    facts: &ProjectFacts,
    module_nodes: &[Option<NodeId>],
    libraries: &HashMap<String, NodeId>,
    module_edges: &[Edge],
) -> Vec<Finding> {
    let node_of_id: HashMap<&str, NodeId> = facts
        .modules
        .iter()
        .zip(module_nodes)
        .filter_map(|(m, n)| n.map(|n| (m.id.as_str(), n)))
        .collect();
    let used: HashSet<(NodeId, NodeId)> = module_edges
        .iter()
        .filter(|e| e.weight.total() > 0)
        .map(|e| (e.from, e.to))
        .collect();

    // Does the dependency travel on to the consumers of a module?
    let transitive = |scope: Scope, exposed: bool| match facts.build_system {
        BuildSystem::Maven => matches!(scope, Scope::Compile | Scope::Runtime),
        BuildSystem::Gradle => exposed,
        BuildSystem::Plain => false,
    };
    let declared: HashMap<NodeId, Vec<(NodeId, bool)>> = facts
        .modules
        .iter()
        .zip(module_nodes)
        .filter_map(|(m, n)| {
            let deps = m
                .module_deps
                .iter()
                .filter_map(|d| {
                    node_of_id
                        .get(d.target.as_str())
                        .map(|t| (*t, transitive(d.scope, d.exposed)))
                })
                .collect();
            n.map(|n| (n, deps))
        })
        .collect();

    let reachable = |from: NodeId, to: NodeId| -> bool {
        // First hop: any declared dependency. Further hops: transitive ones only.
        let mut queue: VecDeque<NodeId> = declared
            .get(&from)
            .into_iter()
            .flatten()
            .map(|(t, _)| *t)
            .collect();
        let mut seen = HashSet::new();
        while let Some(m) = queue.pop_front() {
            if m == to {
                return true;
            }
            if seen.insert(m) {
                queue.extend(
                    declared
                        .get(&m)
                        .into_iter()
                        .flatten()
                        .filter(|(_, t)| *t)
                        .map(|(t, _)| *t),
                );
            }
        }
        false
    };

    let mut out = BTreeSet::new();
    for (module, node) in facts.modules.iter().zip(module_nodes) {
        let Some(node) = *node else { continue };

        // Modules
        let declared_modules: HashSet<NodeId> = declared[&node].iter().map(|(t, _)| *t).collect();
        for &target in &declared_modules {
            if !used.contains(&(node, target)) {
                out.insert((node, 0u8, target, false));
            }
        }
        for &(from, to) in &used {
            if from == node
                && node_of_id.values().any(|m| *m == to)
                && !declared_modules.contains(&to)
            {
                out.insert((node, 2, to, reachable(node, to)));
            }
        }

        // Libraries. Inherited and runtime-only declarations are not reported as
        // unused: the first are a parent's decision, the second never show in code.
        let declared_libraries: HashSet<NodeId> = module
            .external_deps
            .iter()
            .filter_map(|d| libraries.get(&d.coordinates()).copied())
            .collect();
        for dep in &module.external_deps {
            let Some(&library) = libraries.get(&dep.coordinates()) else {
                continue;
            };
            if !dep.inherited && dep.scope != Scope::Runtime && !used.contains(&(node, library)) {
                out.insert((node, 1, library, false));
            }
        }
        for &(from, to) in &used {
            if from == node
                && libraries.values().any(|l| *l == to)
                && !declared_libraries.contains(&to)
            {
                out.insert((node, 3, to, false));
            }
        }
    }

    out.into_iter()
        .map(|(module, kind, target, reachable)| Finding {
            kind: match kind {
                0 => FindingKind::UnusedModuleDependency,
                1 => FindingKind::UnusedLibrary,
                2 => FindingKind::UndeclaredModuleDependency { reachable },
                _ => FindingKind::UndeclaredLibrary,
            },
            module,
            target,
        })
        .collect()
}

// ---------------------------------------------------------------------------
// Metrics
// ---------------------------------------------------------------------------

fn metrics(nodes: &[Node], package_edges: &[Edge], module_edges: &[Edge]) -> Vec<Option<Metrics>> {
    let mut out: Vec<Option<Metrics>> = vec![None; nodes.len()];
    for (edges, is_level) in [
        (
            package_edges,
            (|k: &NodeKind| matches!(k, NodeKind::Package { .. })) as fn(&NodeKind) -> bool,
        ),
        (module_edges, |k: &NodeKind| {
            matches!(k, NodeKind::Module { .. })
        }),
    ] {
        let mut ca: HashMap<NodeId, u32> = HashMap::new();
        let mut ce: HashMap<NodeId, u32> = HashMap::new();
        for e in edges.iter().filter(|e| e.weight.main > 0) {
            let target = &nodes[e.to.index()];
            if matches!(target.kind, NodeKind::Jdk) {
                continue; // like JDepend's default filter: the JDK is everyone's dependency
            }
            *ce.entry(e.from).or_default() += 1;
            if is_level(&target.kind) {
                *ca.entry(e.to).or_default() += 1;
            }
        }
        for n in nodes.iter().filter(|n| is_level(&n.kind) && !n.is_test) {
            out[n.id.index()] = Some(Metrics {
                ca: ca.get(&n.id).copied().unwrap_or(0),
                ce: ce.get(&n.id).copied().unwrap_or(0),
            });
        }
    }
    out
}

#[cfg(test)]
mod tests {
    use super::*;

    fn sorted(mut components: Vec<Vec<usize>>) -> Vec<Vec<usize>> {
        for c in &mut components {
            c.sort();
        }
        components.sort();
        components
    }

    #[test]
    fn tarjan_finds_cycles_and_singletons() {
        // 0 → 1 → 2 → 0 is a cycle; 3 → 4 is not; 5 points into the cycle.
        let graph = vec![vec![1], vec![2], vec![0], vec![4], vec![], vec![0]];
        assert_eq!(
            sorted(tarjan(&graph)),
            vec![vec![0, 1, 2], vec![3], vec![4], vec![5]]
        );
    }

    #[test]
    fn tarjan_on_two_touching_cycles() {
        // 0 ⇄ 1 and 2 ⇄ 3, with 1 → 2 between them: two components, not one.
        let graph = vec![vec![1], vec![0, 2], vec![3], vec![2]];
        assert_eq!(sorted(tarjan(&graph)), vec![vec![0, 1], vec![2, 3]]);
    }

    #[test]
    fn tarjan_handles_self_loops_and_empty_graphs() {
        assert_eq!(sorted(tarjan(&[vec![0]])), vec![vec![0]]);
        assert!(tarjan(&[]).is_empty());
    }

    #[test]
    fn instability() {
        assert_eq!(Metrics { ca: 3, ce: 1 }.instability(), Some(0.25));
        assert_eq!(Metrics { ca: 0, ce: 0 }.instability(), None);
    }
}
