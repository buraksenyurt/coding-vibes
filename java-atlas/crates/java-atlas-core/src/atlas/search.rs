//! Finding nodes by name: the search box.

use super::{Atlas, NodeId, NodeKind};

impl Atlas {
    /// Nodes whose name or full name contains `query` (case-insensitive),
    /// best matches first, at most `limit`.
    ///
    /// Ranking, best first: exact name, name prefix, name contains, full
    /// name contains. Within a rank, broader things come first (a module
    /// before a package before a type), then shorter names, then by name.
    pub fn search(&self, query: &str, limit: usize) -> Vec<NodeId> {
        let q = query.trim().to_lowercase();
        if q.is_empty() {
            return Vec::new();
        }
        let kind_order = |k: &NodeKind| match k {
            NodeKind::Module { .. } => 0,
            NodeKind::Package { .. } => 1,
            NodeKind::Type { .. } => 2,
            NodeKind::Library { .. } => 3,
            NodeKind::Unknown => 4,
            NodeKind::Jdk => 5,
        };
        let mut hits: Vec<(u8, u8, usize, &str, NodeId)> = self
            .nodes
            .iter()
            .filter_map(|n| {
                let name = n.name.to_lowercase();
                let rank = if name == q {
                    0
                } else if name.starts_with(&q) {
                    1
                } else if name.contains(&q) {
                    2
                } else if n.fqn.to_lowercase().contains(&q) {
                    3
                } else {
                    return None;
                };
                Some((
                    rank,
                    kind_order(&n.kind),
                    n.name.len(),
                    n.name.as_str(),
                    n.id,
                ))
            })
            .collect();
        hits.sort();
        hits.into_iter().take(limit).map(|h| h.4).collect()
    }
}
