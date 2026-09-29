//! Facts about branches for the side bar: ahead/behind, merged, stale,
//! remote sync. Everything is computed on the metro map's commit window, so
//! on a truncated history the counts are lower bounds.

use std::collections::BTreeSet;

use crate::{Column, GitRef, Head, LaneId, MetroMap, RefKind};

/// Days without a commit after which an unmerged branch counts as stale.
pub const DEFAULT_STALE_DAYS: i64 = 30;

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct BranchStats {
    /// `main`, `feature/login`, or `origin/x` for a remote-only branch.
    pub name: String,
    pub is_remote_only: bool,
    /// Lane that carries the branch's own commits, if it has any.
    pub lane: Option<LaneId>,
    pub tip_column: Column,
    /// Commits on this branch that the base branch does not have, and vice versa.
    pub ahead: usize,
    pub behind: usize,
    /// The tip is reachable from the base branch.
    pub merged: bool,
    /// Unmerged and quiet for longer than the threshold.
    pub stale: bool,
    /// Committer time of the tip, seconds since the epoch.
    pub last_activity: i64,
    /// Commits on the branch's own lane.
    pub commit_count: usize,
    /// Distinct authors on the branch's own lane, most active first.
    pub authors: Vec<String>,
    /// Remote-tracking twin, e.g. `origin/main`, with ahead/behind against it.
    pub upstream: Option<Upstream>,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Upstream {
    pub name: String,
    pub ahead: usize,
    pub behind: usize,
}

/// The branch everything else is compared with: `main`, else `master`,
/// else whatever HEAD is on, else the first local branch.
pub fn base_branch(refs: &[&GitRef], head: &Head) -> Option<String> {
    let local: Vec<&str> = refs
        .iter()
        .filter(|r| r.kind == RefKind::LocalBranch)
        .map(|r| r.name.as_str())
        .collect();
    ["main", "master"]
        .into_iter()
        .find(|n| local.contains(n))
        .map(str::to_owned)
        .or_else(|| match head {
            Head::Branch { name, .. } => Some(name.clone()),
            _ => None,
        })
        .or_else(|| local.first().map(|n| (*n).to_owned()))
}

pub fn branch_stats(map: &MetroMap, now: i64, stale_days: i64) -> Vec<BranchStats> {
    let refs: Vec<&GitRef> = map.refs_at.values().flatten().collect();
    let tip_of = |name: &str| {
        refs.iter()
            .find(|r| r.name == name && r.kind != RefKind::Tag)
            .and_then(|r| map.column_of(&r.target))
    };
    let base = base_branch(&refs, &map.head);
    let base_reach = base
        .as_deref()
        .and_then(tip_of)
        .map(|tip| reachable(map, tip));

    let locals: BTreeSet<&str> = refs
        .iter()
        .filter(|r| r.kind == RefKind::LocalBranch)
        .map(|r| r.name.as_str())
        .collect();

    let mut stats = Vec::new();
    for r in refs.iter().filter(|r| r.is_branch()) {
        let is_remote = matches!(r.kind, RefKind::RemoteBranch { .. });
        // A remote branch with a local twin is reported as the twin's upstream.
        if is_remote && locals.contains(r.branch_name()) {
            continue;
        }
        let Some(tip) = map.column_of(&r.target) else {
            continue;
        };
        let reach = reachable(map, tip);

        let (ahead, behind, merged) = match &base_reach {
            Some(base) if base.len() == reach.len() => {
                let ahead = count(&reach, |i| !base[i]);
                let behind = count(base, |i| !reach[i]);
                (ahead, behind, base[tip])
            }
            _ => (0, 0, false),
        };

        let lane = map
            .lanes
            .iter()
            .find(|l| l.refs.iter().any(|n| n == &r.name))
            .map(|l| l.id);
        let own: Vec<Column> = match lane {
            Some(id) => (0..map.commits.len())
                .filter(|&c| map.lane_of[c] == id)
                .collect(),
            None => Vec::new(),
        };

        let last_activity = map.commits[tip].committer.time.seconds;
        let is_base = base.as_deref() == Some(r.name.as_str());
        let stale = !is_base && !merged && now - last_activity > stale_days * 86_400;

        let upstream = (!is_remote)
            .then(|| {
                refs.iter().find(|u| {
                    matches!(u.kind, RefKind::RemoteBranch { .. }) && u.branch_name() == r.name
                })
            })
            .flatten()
            .and_then(|u| {
                let up_tip = map.column_of(&u.target)?;
                let up = reachable(map, up_tip);
                Some(Upstream {
                    name: u.name.clone(),
                    ahead: count(&reach, |i| !up[i]),
                    behind: count(&up, |i| !reach[i]),
                })
            });

        stats.push(BranchStats {
            name: r.name.clone(),
            is_remote_only: is_remote,
            lane,
            tip_column: tip,
            ahead,
            behind,
            merged: merged && !is_base,
            stale,
            last_activity,
            commit_count: own.len(),
            authors: authors(map, &own),
            upstream,
        });
    }
    stats
}

/// Every commit reachable from `tip` inside the window, as a bitmap indexed
/// by column. A `Vec<bool>` beats a `HashSet` here: columns are dense.
pub fn reachable(map: &MetroMap, tip: Column) -> Vec<bool> {
    let mut seen = vec![false; map.commits.len()];
    let mut stack = vec![tip];
    while let Some(column) = stack.pop() {
        if std::mem::replace(&mut seen[column], true) {
            continue;
        }
        stack.extend(map.parents_of(column).filter(|&p| !seen[p]));
    }
    seen
}

/// Names of branches (local and remote) whose tip can reach `column`.
pub fn branches_containing(map: &MetroMap, column: Column) -> Vec<String> {
    let mut names: Vec<String> = map
        .refs_at
        .iter()
        .flat_map(|(&tip, refs)| refs.iter().map(move |r| (tip, r)))
        .filter(|(_, r)| r.is_branch())
        .filter(|(tip, _)| *tip >= column && reachable(map, *tip)[column])
        .map(|(_, r)| r.name.clone())
        .collect();
    names.sort();
    names
}

fn count(bits: &[bool], keep: impl Fn(usize) -> bool) -> usize {
    bits.iter()
        .enumerate()
        .filter(|&(i, &b)| b && keep(i))
        .count()
}

fn authors(map: &MetroMap, columns: &[Column]) -> Vec<String> {
    let mut tally: Vec<(String, usize)> = Vec::new();
    for &c in columns {
        let name = &map.commits[c].author.name;
        match tally.iter_mut().find(|(n, _)| n == name) {
            Some((_, n)) => *n += 1,
            None => tally.push((name.clone(), 1)),
        }
    }
    tally.sort_by(|a, b| b.1.cmp(&a.1).then_with(|| a.0.cmp(&b.0)));
    tally.into_iter().map(|(name, _)| name).collect()
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::testing::FakeHistory;

    fn stats_of<'a>(all: &'a [BranchStats], name: &str) -> &'a BranchStats {
        all.iter().find(|s| s.name == name).unwrap()
    }

    #[test]
    fn ahead_behind_and_merged() {
        let mut h = FakeHistory::new();
        let a = h.commit("a", &[]);
        let f1 = h.commit("f1", &[a]);
        let f2 = h.commit("f2", &[f1]);
        let b = h.commit("b", &[a]);
        let g1 = h.commit("g1", &[b]);
        let m = h.commit("merge g", &[b, g1]);
        h.branch("main", m);
        h.branch("feature/f", f2);
        h.branch("feature/g", g1);
        let map = h.build();
        let all = branch_stats(&map, 1_700_000_000, 30);

        let f = stats_of(&all, "feature/f");
        assert_eq!((f.ahead, f.behind, f.merged), (2, 3, false));
        assert_eq!(f.commit_count, 2);

        let g = stats_of(&all, "feature/g");
        assert_eq!((g.ahead, g.merged), (0, true));

        let main = stats_of(&all, "main");
        assert_eq!((main.ahead, main.behind, main.merged), (0, 0, false));
    }

    #[test]
    fn stale_means_unmerged_and_quiet() {
        let mut h = FakeHistory::new();
        let a = h.commit("a", &[]);
        let f = h.commit("f", &[a]);
        h.branch("main", a);
        h.branch("feature/old", f);
        let map = h.build();
        let later = map.commits[1].committer.time.seconds + 31 * 86_400;
        assert!(stats_of(&branch_stats(&map, later, 30), "feature/old").stale);
        assert!(!stats_of(&branch_stats(&map, later, 30), "main").stale);
        assert!(!stats_of(&branch_stats(&map, later - 2 * 86_400, 30), "feature/old").stale);
    }

    #[test]
    fn remote_twin_becomes_upstream() {
        let mut h = FakeHistory::new();
        let a = h.commit("a", &[]);
        let b = h.commit("b", &[a]);
        h.branch("main", b);
        h.remote_branch("origin", "main", a);
        h.remote_branch("origin", "feature/remote-only", a);
        let map = h.build();
        let all = branch_stats(&map, 0, 30);

        let main = stats_of(&all, "main");
        let up = main.upstream.as_ref().unwrap();
        assert_eq!(
            (up.name.as_str(), up.ahead, up.behind),
            ("origin/main", 1, 0)
        );
        assert!(all.iter().all(|s| s.name != "origin/main"));
        assert!(stats_of(&all, "origin/feature/remote-only").is_remote_only);
    }

    #[test]
    fn finds_branches_containing_a_commit() {
        let mut h = FakeHistory::new();
        let a = h.commit("a", &[]);
        let f = h.commit("f", &[a]);
        h.branch("main", a);
        h.branch("feature/f", f);
        let map = h.build();
        assert_eq!(branches_containing(&map, 0), ["feature/f", "main"]);
        assert_eq!(branches_containing(&map, 1), ["feature/f"]);
    }
}
