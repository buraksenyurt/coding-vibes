//! What one screen of the map shows: the nodes and roads of one scale.
//!
//! The front end never receives the whole atlas — a large project has
//! thousands of types. It asks for one *view*: a scale (country, region,
//! city), an optional focus (which region or city) and filters. This module
//! picks the nodes and folds the edges so that every road connects two things
//! that are actually drawn.

use std::collections::{BTreeMap, BTreeSet, HashMap};

use super::{Atlas, Edge, Evidence, NodeId, NodeKind, Weight};
use crate::Scale;

/// What to leave off the map.
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub struct ViewFilters {
    /// Test code (construction sites).
    pub tests: bool,
    /// JDK packages (the mainland).
    pub jdk: bool,
    /// Libraries and unknown packages (islands).
    pub external: bool,
}

impl Default for ViewFilters {
    /// The calm default: production code and its libraries, no JDK, no tests.
    fn default() -> Self {
        ViewFilters {
            tests: false,
            jdk: false,
            external: true,
        }
    }
}

/// A road between two nodes of the view.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct ViewEdge {
    pub from: NodeId,
    pub to: NodeId,
    pub weight: Weight,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct View {
    pub scale: Scale,
    pub focus: Option<NodeId>,
    /// The nodes the scale is *about* (regions, cities or buildings), sorted.
    pub members: Vec<NodeId>,
    /// Nodes drawn for context: other regions, islands, the mainland. Sorted.
    pub neighbours: Vec<NodeId>,
    pub edges: Vec<ViewEdge>,
}

impl Atlas {
    /// A stable, human-readable key for a node. Indices change between scans;
    /// keys do not, so the UI can keep a selection across a rescan.
    pub fn key(&self, id: NodeId) -> String {
        let n = self.node(id);
        match &n.kind {
            NodeKind::Module { .. } => format!("m:{}", n.fqn),
            NodeKind::Package { .. } => {
                let module = n
                    .parent
                    .map(|m| self.node(m).fqn.as_str())
                    .unwrap_or_default();
                format!("p:{module}|{}", n.fqn)
            }
            NodeKind::Type { .. } => format!("t:{}", n.fqn),
            NodeKind::Library { .. } => format!("l:{}", n.fqn),
            NodeKind::Jdk => format!("j:{}", n.fqn),
            NodeKind::Unknown => format!("u:{}", n.fqn),
        }
    }

    /// All keys, for looking nodes up by key.
    pub fn key_index(&self) -> HashMap<String, NodeId> {
        self.nodes.iter().map(|n| (self.key(n.id), n.id)).collect()
    }

    /// Is `ancestor` the node itself or one of its parents?
    pub fn contains(&self, ancestor: NodeId, mut id: NodeId) -> bool {
        loop {
            if id == ancestor {
                return true;
            }
            match self.node(id).parent {
                Some(parent) => id = parent,
                None => return false,
            }
        }
    }

    /// Lines of code under each node (a type's own lines; sums for packages
    /// and modules). Sizes the regions, cities and buildings.
    pub fn sizes(&self) -> Vec<u32> {
        let mut sizes = vec![0u32; self.nodes.len()];
        for n in &self.nodes {
            if let NodeKind::Type { lines, .. } = n.kind {
                let mut current = Some(n.id);
                while let Some(id) = current {
                    sizes[id.index()] += lines;
                    current = self.node(id).parent;
                }
            }
        }
        sizes
    }

    /// Every piece of evidence behind a road between `from` and `to`, whatever
    /// their level (`package → module`, `type → library`…): all type-level
    /// edges whose ends lie inside them. Sorted, at most `limit`.
    pub fn evidence_between(&self, from: NodeId, to: NodeId, limit: usize) -> Vec<Evidence> {
        let mut found = BTreeSet::new();
        for e in &self.type_edges {
            if self.contains(from, e.from) && self.contains(to, e.to) {
                found.extend(e.evidence.iter().cloned());
            }
        }
        found.into_iter().take(limit).collect()
    }

    /// The nodes and roads of one screen.
    ///
    /// | Scale | Members | Roads come from | Other ends are folded to |
    /// |---|---|---|---|
    /// | Country | modules | module edges | — |
    /// | Region (module) | its packages | package edges touching it | other packages → their module |
    /// | City (package) | its types | type edges touching it | other types → their package |
    pub fn view(&self, scale: Scale, focus: Option<NodeId>, filters: ViewFilters) -> View {
        let is_member = |id: NodeId| -> bool {
            let n = self.node(id);
            let right_kind = match scale {
                Scale::Country => matches!(n.kind, NodeKind::Module { .. }),
                Scale::Region => matches!(n.kind, NodeKind::Package { .. }),
                Scale::City => matches!(n.kind, NodeKind::Type { .. }),
            };
            right_kind && focus.is_none_or(|f| n.parent == Some(f)) && (filters.tests || !n.is_test)
        };
        // Internal nodes outside the focus are shown one level up: a package of
        // another module appears as that module's region at the border.
        let fold = |id: NodeId| -> NodeId {
            if is_member(id) || !self.node(id).is_internal() {
                return id;
            }
            let wanted: fn(&NodeKind) -> bool = match scale {
                Scale::Country | Scale::Region => {
                    |k: &NodeKind| matches!(k, NodeKind::Module { .. })
                }
                Scale::City => |k: &NodeKind| matches!(k, NodeKind::Package { .. }),
            };
            let mut current = id;
            while !wanted(&self.node(current).kind) {
                match self.node(current).parent {
                    Some(p) => current = p,
                    None => break,
                }
            }
            current
        };
        let shown = |id: NodeId| -> bool {
            match self.node(id).kind {
                NodeKind::Jdk => filters.jdk,
                NodeKind::Library { .. } | NodeKind::Unknown => filters.external,
                _ => filters.tests || !self.node(id).is_test,
            }
        };

        let source: &[Edge] = match scale {
            Scale::Country => &self.module_edges,
            Scale::Region => &self.package_edges,
            Scale::City => &self.type_edges,
        };

        let mut merged: BTreeMap<(NodeId, NodeId), Weight> = BTreeMap::new();
        for e in source {
            // Only roads that start or end inside the focus.
            if !(is_member(e.from) || is_member(e.to)) {
                continue;
            }
            let weight = if filters.tests {
                e.weight
            } else {
                Weight {
                    main: e.weight.main,
                    test: 0,
                }
            };
            if weight.total() == 0 {
                continue;
            }
            let (from, to) = (fold(e.from), fold(e.to));
            if from == to || !shown(from) || !shown(to) {
                continue;
            }
            let w = merged.entry((from, to)).or_default();
            w.main += weight.main;
            w.test += weight.test;
        }

        let members: Vec<NodeId> = self
            .nodes
            .iter()
            .map(|n| n.id)
            .filter(|id| is_member(*id))
            .collect();
        let mut neighbours: BTreeSet<NodeId> = merged
            .keys()
            .flat_map(|(a, b)| [*a, *b])
            .filter(|id| !is_member(*id))
            .collect();
        // At country scale every island is drawn, used or not: an unused
        // library is a finding worth seeing.
        if scale == Scale::Country && filters.external {
            neighbours.extend(
                self.nodes
                    .iter()
                    .filter(|n| matches!(n.kind, NodeKind::Library { .. }))
                    .map(|n| n.id),
            );
        }

        View {
            scale,
            focus,
            members,
            neighbours: neighbours.into_iter().collect(),
            edges: merged
                .into_iter()
                .map(|((from, to), weight)| ViewEdge { from, to, weight })
                .collect(),
        }
    }
}
