//! The metro map: commits laid out on lanes, plus the transitions between lanes.
//!
//! Graph storage uses plain indices into vectors ("arena" style). A commit is
//! identified by its column, which is also its index in `MetroMap::commits`.
//! No `Rc<RefCell<_>>`, no lifetimes tying nodes together, nothing for the
//! borrow checker to argue about.

use std::cmp::Reverse;
use std::collections::{BTreeMap, BinaryHeap, HashMap};

use crate::infer::branch_from_merge_message;
use crate::{BranchPriority, Commit, CommitId, GitRef, Head, HistorySource, RefKind, SourceError};

/// Index of a commit in `MetroMap::commits`; doubles as its X column.
pub type Column = usize;
/// Index of a lane in `MetroMap::lanes`.
pub type LaneId = usize;

/// How many distinct lane colours the UI palette offers.
pub const PALETTE_SIZE: u8 = 8;

#[derive(Debug, Clone, PartialEq, Eq)]
pub enum LaneLabel {
    /// A living ref points into this lane: `main`, `origin/feature/x`.
    Named(String),
    /// Recovered from a merge message; the branch itself was deleted.
    Inferred(String),
    /// Nothing tells us what this line of work was called.
    Anonymous,
}

impl LaneLabel {
    pub fn text(&self) -> &str {
        match self {
            LaneLabel::Named(name) | LaneLabel::Inferred(name) => name,
            LaneLabel::Anonymous => "(anonymous)",
        }
    }
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Lane {
    pub id: LaneId,
    pub label: LaneLabel,
    /// Vertical position, 0 at the top: one row per lane.
    pub row: usize,
    /// Row in the compact view, where lanes that never overlap share a row.
    pub compact_row: usize,
    /// Index into the UI palette. `main`/`master` always gets 0.
    pub color: u8,
    /// Local and remote refs whose history lives on this lane.
    pub refs: Vec<String>,
    /// Oldest and newest commit that belong to the lane.
    pub first_column: Column,
    pub last_column: Column,
    /// Column of the commit this lane forked from, if it is in the window.
    pub fork_column: Option<Column>,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, PartialOrd, Ord)]
pub enum TransitionKind {
    /// A lane starts from a commit on another lane.
    Fork,
    /// A merge commit pulls in a commit from another lane.
    Merge,
    /// A commit was copied with `git cherry-pick -x`.
    CherryPick,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash)]
pub struct Transition {
    pub kind: TransitionKind,
    pub from_lane: LaneId,
    pub from_column: Column,
    pub to_lane: LaneId,
    pub to_column: Column,
}

#[derive(Debug, Clone)]
pub struct MapOptions {
    /// Newest commits to read. Older history is cut off.
    pub limit: usize,
    pub priority: BranchPriority,
}

impl Default for MapOptions {
    fn default() -> Self {
        Self {
            limit: 1500,
            priority: BranchPriority::default(),
        }
    }
}

#[derive(Debug, Clone)]
pub struct MetroMap {
    /// Topological order, oldest first. The index is the column.
    pub commits: Vec<Commit>,
    /// Lane of each commit, parallel to `commits`.
    pub lane_of: Vec<LaneId>,
    pub lanes: Vec<Lane>,
    pub transitions: Vec<Transition>,
    /// Refs grouped by the column they point at.
    pub refs_at: BTreeMap<Column, Vec<GitRef>>,
    pub head: Head,
    /// True when the limit cut older history off.
    pub truncated: bool,
    index: HashMap<CommitId, Column>,
}

impl MetroMap {
    pub fn column_of(&self, id: &CommitId) -> Option<Column> {
        self.index.get(id).copied()
    }

    pub fn lane(&self, id: LaneId) -> &Lane {
        &self.lanes[id]
    }

    pub fn lane_named(&self, name: &str) -> Option<&Lane> {
        self.lanes.iter().find(|l| l.label.text() == name)
    }

    /// Columns of the in-window parents of a commit.
    pub fn parents_of(&self, column: Column) -> impl Iterator<Item = Column> + '_ {
        self.commits[column]
            .parents
            .iter()
            .filter_map(|p| self.column_of(p))
    }
}

/// Reads history from `source` and lays it out. The heart of easy-git.
pub fn build_metro_map(
    source: &impl HistorySource,
    options: &MapOptions,
) -> Result<MetroMap, SourceError> {
    let refs = source.refs()?;
    let head = source.head()?;

    let mut tips: Vec<CommitId> = refs.iter().map(|r| r.target).collect();
    tips.extend(head.target());
    tips.sort();
    tips.dedup();

    let walked = if tips.is_empty() {
        Vec::new()
    } else {
        source.walk(&tips, options.limit)?
    };
    let truncated = walked.len() >= options.limit;
    Ok(layout(walked, refs, head, &options.priority, truncated))
}

/// Pure function from raw history to a map. Split from `build_metro_map` so
/// tests can feed it commits directly.
pub fn layout(
    walked: Vec<Commit>,
    refs: Vec<GitRef>,
    head: Head,
    priority: &BranchPriority,
    truncated: bool,
) -> MetroMap {
    let commits = topological_order(walked);
    let index: HashMap<CommitId, Column> =
        commits.iter().enumerate().map(|(i, c)| (c.id, i)).collect();
    let first_parent: Vec<Option<Column>> = commits
        .iter()
        .map(|c| c.parents.first().and_then(|p| index.get(p).copied()))
        .collect();

    let mut builder = LaneBuilder {
        lane_of: vec![None; commits.len()],
        lanes: Vec::new(),
        first_parent: &first_parent,
    };

    // Step 1 and 2: named branches claim history along first-parent chains.
    for candidate in branch_candidates(&refs, &head, priority, &index) {
        builder.claim(
            LaneLabel::Named(candidate.label),
            candidate.refs,
            &candidate.tips,
        );
    }

    // Step 3: what is left belongs to deleted branches. Newest first, so the
    // first unclaimed commit we meet is the tip of such a branch.
    for column in (0..commits.len()).rev() {
        if builder.lane_of[column].is_some() {
            continue;
        }
        let label = merged_by(column, &commits)
            .and_then(|merge| branch_from_merge_message(&commits[merge].summary))
            .map(LaneLabel::Inferred)
            .unwrap_or(LaneLabel::Anonymous);
        builder.claim(label, Vec::new(), &[column]);
    }

    let LaneBuilder {
        lane_of, mut lanes, ..
    } = builder;
    let lane_of: Vec<LaneId> = lane_of
        .into_iter()
        .map(|l| l.expect("every commit is claimed"))
        .collect();

    // Step 4 and 5: transitions follow from lanes and parents.
    let transitions = transitions(&commits, &lane_of, &index);
    for t in &transitions {
        if t.kind == TransitionKind::Fork && lanes[t.to_lane].first_column == t.to_column {
            lanes[t.to_lane].fork_column = Some(t.from_column);
        }
    }

    assign_rows_and_colors(&mut lanes);
    compact_rows(&mut lanes, &transitions);

    let mut refs_at: BTreeMap<Column, Vec<GitRef>> = BTreeMap::new();
    for r in refs {
        if let Some(&column) = index.get(&r.target) {
            refs_at.entry(column).or_default().push(r);
        }
    }

    MetroMap {
        commits,
        lane_of,
        lanes,
        transitions,
        refs_at,
        head,
        truncated,
        index,
    }
}

/// Kahn's algorithm: a commit is placed only after all its (in-window)
/// parents. Among the commits that are ready, the oldest committer time goes
/// first, so the X axis still roughly follows the clock without ever letting a
/// skewed clock put a child before its parent.
fn topological_order(mut commits: Vec<Commit>) -> Vec<Commit> {
    let index: HashMap<CommitId, usize> =
        commits.iter().enumerate().map(|(i, c)| (c.id, i)).collect();
    let mut pending = vec![0usize; commits.len()];
    let mut children: Vec<Vec<usize>> = vec![Vec::new(); commits.len()];
    for (i, commit) in commits.iter().enumerate() {
        for parent in &commit.parents {
            if let Some(&p) = index.get(parent) {
                pending[i] += 1;
                children[p].push(i);
            }
        }
    }

    let key = |i: usize, commits: &[Commit]| {
        Reverse((commits[i].committer.time.seconds, commits[i].id, i))
    };
    let mut ready: BinaryHeap<_> = (0..commits.len())
        .filter(|&i| pending[i] == 0)
        .map(|i| key(i, &commits))
        .collect();
    let mut order = Vec::with_capacity(commits.len());
    while let Some(Reverse((_, _, i))) = ready.pop() {
        order.push(i);
        for &child in &children[i] {
            pending[child] -= 1;
            if pending[child] == 0 {
                ready.push(key(child, &commits));
            }
        }
    }

    // Move commits out of the old vector without cloning them.
    let mut slots: Vec<Option<Commit>> = commits.drain(..).map(Some).collect();
    order
        .into_iter()
        .map(|i| slots[i].take().expect("each index appears once"))
        .collect()
}

struct Candidate {
    label: String,
    refs: Vec<String>,
    tips: Vec<Column>,
}

/// Groups refs into lanes-to-be: `main` and `origin/main` share one lane.
/// Remote branches without a local twin become their own candidate.
fn branch_candidates(
    refs: &[GitRef],
    head: &Head,
    priority: &BranchPriority,
    index: &HashMap<CommitId, Column>,
) -> Vec<Candidate> {
    // key: (remote_only, rank, name)
    let mut groups: BTreeMap<String, (bool, Vec<&GitRef>)> = BTreeMap::new();
    for r in refs.iter().filter(|r| r.kind == RefKind::LocalBranch) {
        groups
            .entry(r.name.clone())
            .or_insert((false, Vec::new()))
            .1
            .push(r);
    }
    for r in refs
        .iter()
        .filter(|r| matches!(r.kind, RefKind::RemoteBranch { .. }))
    {
        match groups.get_mut(r.branch_name()) {
            Some((false, members)) => members.push(r),
            _ => groups
                .entry(r.name.clone())
                .or_insert((true, Vec::new()))
                .1
                .push(r),
        }
    }

    let mut candidates: Vec<(bool, usize, Candidate)> = groups
        .into_iter()
        .map(|(label, (remote_only, members))| {
            let rank_name = members
                .first()
                .map(|r| r.branch_name().to_owned())
                .unwrap_or_else(|| label.clone());
            let tips = members
                .iter()
                .filter_map(|r| index.get(&r.target).copied())
                .collect();
            let refs = members.iter().map(|r| r.name.clone()).collect();
            (
                remote_only,
                priority.rank(&rank_name),
                Candidate { label, refs, tips },
            )
        })
        .collect();
    candidates.sort_by(|a, b| (a.0, a.1, &a.2.label).cmp(&(b.0, b.1, &b.2.label)));

    let mut result: Vec<Candidate> = candidates.into_iter().map(|(_, _, c)| c).collect();
    if let Head::Detached(id) = head {
        if let Some(&column) = index.get(id) {
            result.push(Candidate {
                label: "HEAD".into(),
                refs: vec!["HEAD".into()],
                tips: vec![column],
            });
        }
    }
    result
}

struct LaneBuilder<'a> {
    lane_of: Vec<Option<LaneId>>,
    lanes: Vec<Lane>,
    first_parent: &'a [Option<Column>],
}

impl LaneBuilder<'_> {
    /// Walks first parents from each tip, claiming unclaimed commits, and
    /// stops at the first commit someone else already owns (the fork point).
    /// A candidate that claims nothing produces no lane: its ref simply sits
    /// on another lane's station.
    fn claim(&mut self, label: LaneLabel, refs: Vec<String>, tips: &[Column]) {
        let id = self.lanes.len();
        let mut claimed: Vec<Column> = Vec::new();
        for &tip in tips {
            let mut cursor = Some(tip);
            while let Some(column) = cursor {
                if self.lane_of[column].is_some() {
                    break;
                }
                self.lane_of[column] = Some(id);
                claimed.push(column);
                cursor = self.first_parent[column];
            }
        }
        if claimed.is_empty() {
            return;
        }
        self.lanes.push(Lane {
            id,
            label,
            row: id,
            compact_row: id,
            color: 0,
            refs,
            first_column: *claimed.iter().min().expect("not empty"),
            last_column: *claimed.iter().max().expect("not empty"),
            fork_column: None,
        });
    }
}

/// The merge commit whose second (or later) parent is `column`.
fn merged_by(column: Column, commits: &[Commit]) -> Option<Column> {
    let id = commits[column].id;
    commits
        .iter()
        .enumerate()
        .skip(column + 1)
        .find(|(_, c)| c.parents.iter().skip(1).any(|p| *p == id))
        .map(|(i, _)| i)
}

fn transitions(
    commits: &[Commit],
    lane_of: &[LaneId],
    index: &HashMap<CommitId, Column>,
) -> Vec<Transition> {
    let mut result = Vec::new();
    for (to, commit) in commits.iter().enumerate() {
        for (n, parent) in commit.parents.iter().enumerate() {
            let Some(&from) = index.get(parent) else {
                continue;
            };
            if lane_of[from] == lane_of[to] {
                continue;
            }
            let kind = if n == 0 {
                TransitionKind::Fork
            } else {
                TransitionKind::Merge
            };
            result.push(Transition {
                kind,
                from_lane: lane_of[from],
                from_column: from,
                to_lane: lane_of[to],
                to_column: to,
            });
        }
        if let Some(&from) = cherry_pick_source(&commit.message).and_then(|id| index.get(&id)) {
            if lane_of[from] != lane_of[to] {
                result.push(Transition {
                    kind: TransitionKind::CherryPick,
                    from_lane: lane_of[from],
                    from_column: from,
                    to_lane: lane_of[to],
                    to_column: to,
                });
            }
        }
    }
    result
}

/// `git cherry-pick -x` appends "(cherry picked from commit <id>)".
fn cherry_pick_source(message: &str) -> Option<CommitId> {
    message.lines().rev().find_map(|line| {
        line.trim()
            .strip_prefix("(cherry picked from commit ")?
            .strip_suffix(')')?
            .parse()
            .ok()
    })
}

/// One row per lane for now (row compaction arrives in phase 5). Colours:
/// `main`/`master` is always 0, the rest cycle through the palette.
fn assign_rows_and_colors(lanes: &mut [Lane]) {
    let mut next = 1u8;
    for (row, lane) in lanes.iter_mut().enumerate() {
        lane.row = row;
        let is_trunk = matches!(&lane.label, LaneLabel::Named(n) if n == "main" || n == "master");
        lane.color = if is_trunk {
            0
        } else {
            let color = next;
            next = next % (PALETTE_SIZE - 1) + 1;
            color
        };
    }
}

/// Greedy interval partitioning. A lane occupies its own row from the column
/// after its fork (the fork curve lands there) to the column before the merge
/// it flows into (the merge curve leaves there). Lanes are placed, in priority
/// order, on the first row where they collide with nothing. Priority order
/// keeps `main` on row 0 and long-lived branches near the top.
fn compact_rows(lanes: &mut [Lane], transitions: &[Transition]) {
    /// Empty columns kept between two lanes sharing a row.
    const GAP: usize = 1;

    let span = |lane: &Lane| {
        let start = lane
            .fork_column
            .map_or(lane.first_column, |fork| fork + 1)
            .min(lane.first_column);
        let end = transitions
            .iter()
            .filter(|t| t.from_lane == lane.id && t.kind == TransitionKind::Merge)
            .map(|t| t.to_column - 1)
            .fold(lane.last_column, usize::max);
        (start, end)
    };

    let mut rows: Vec<Vec<(usize, usize)>> = Vec::new();
    for lane in lanes.iter_mut() {
        let (start, end) = span(lane);
        let free = |taken: &Vec<(usize, usize)>| {
            taken.iter().all(|&(s, e)| end + GAP < s || e + GAP < start)
        };
        let row = match rows.iter().position(free) {
            Some(row) => row,
            None => {
                rows.push(Vec::new());
                rows.len() - 1
            }
        };
        rows[row].push((start, end));
        lane.compact_row = row;
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::testing::FakeHistory;

    #[test]
    fn single_branch_is_one_lane() {
        let mut h = FakeHistory::new();
        let a = h.commit("a", &[]);
        let b = h.commit("b", &[a]);
        h.branch("main", b);
        let map = h.build();

        assert_eq!(map.lanes.len(), 1);
        assert_eq!(map.lanes[0].label, LaneLabel::Named("main".into()));
        assert!(map.transitions.is_empty());
        assert_eq!(map.commits[0].summary, "a");
    }

    #[test]
    fn feature_branch_forks_and_merges_back() {
        let mut h = FakeHistory::new();
        let a = h.commit("a", &[]);
        let f1 = h.commit("f1", &[a]);
        let b = h.commit("b", &[a]);
        let m = h.commit("merge", &[b, f1]);
        h.branch("main", m);
        h.branch("feature/x", f1);
        let map = h.build();

        assert_eq!(map.lanes.len(), 2);
        let feature = map.lane_named("feature/x").unwrap();
        assert_eq!(feature.fork_column, map.column_of(&a));
        let kinds: Vec<_> = map.transitions.iter().map(|t| t.kind).collect();
        assert_eq!(kinds, [TransitionKind::Fork, TransitionKind::Merge]);
    }

    #[test]
    fn priority_decides_ownership_of_shared_history() {
        // `develop` and `main` point at the same chain; main wins.
        let mut h = FakeHistory::new();
        let a = h.commit("a", &[]);
        let b = h.commit("b", &[a]);
        let c = h.commit("c", &[b]);
        h.branch("develop", c);
        h.branch("main", b);
        let map = h.build();

        assert_eq!(
            map.lane_of[map.column_of(&b).unwrap()],
            map.lane_named("main").unwrap().id
        );
        assert_eq!(
            map.lane_of[map.column_of(&c).unwrap()],
            map.lane_named("develop").unwrap().id
        );
    }

    #[test]
    fn deleted_branch_gets_an_inferred_lane() {
        let mut h = FakeHistory::new();
        let a = h.commit("a", &[]);
        let s = h.commit("search", &[a]);
        let m = h.commit("Merge branch 'feature/search'", &[a, s]);
        h.branch("main", m);
        let map = h.build();

        assert!(
            map.lanes
                .iter()
                .any(|l| l.label == LaneLabel::Inferred("feature/search".into()))
        );
    }

    #[test]
    fn unknown_deleted_branch_is_anonymous() {
        let mut h = FakeHistory::new();
        let a = h.commit("a", &[]);
        let s = h.commit("work", &[a]);
        let m = h.commit("Merged PR 7: work", &[a, s]);
        h.branch("main", m);
        assert!(
            h.build()
                .lanes
                .iter()
                .any(|l| l.label == LaneLabel::Anonymous)
        );
    }

    #[test]
    fn remote_twin_shares_the_local_lane() {
        let mut h = FakeHistory::new();
        let a = h.commit("a", &[]);
        let b = h.commit("b", &[a]);
        h.branch("main", b);
        h.remote_branch("origin", "main", a);
        let map = h.build();
        assert_eq!(map.lanes.len(), 1);
        assert_eq!(map.lanes[0].refs, ["main", "origin/main"]);
    }

    #[test]
    fn branch_without_own_commits_makes_no_lane() {
        let mut h = FakeHistory::new();
        let a = h.commit("a", &[]);
        h.branch("main", a);
        h.branch("feature/empty", a);
        let map = h.build();
        assert_eq!(map.lanes.len(), 1);
        assert_eq!(map.refs_at[&0].len(), 2);
    }

    #[test]
    fn parents_outside_the_window_are_tolerated() {
        let mut h = FakeHistory::new();
        let outside: CommitId = "ffffffffffffffffffffffffffffffffffffffff".parse().unwrap();
        let a = h.commit("a", &[outside]);
        let b = h.commit("b", &[a]);
        h.branch("main", b);
        let map = h.build();
        assert_eq!(map.commits.len(), 2);
        assert!(map.transitions.is_empty());
    }

    #[test]
    fn children_never_precede_parents_even_with_skewed_clocks() {
        let mut h = FakeHistory::new();
        let a = h.commit("a", &[]);
        let b = h.commit_at("b (clock in the past)", &[a], -10_000);
        h.branch("main", b);
        let map = h.build();
        assert!(map.column_of(&a).unwrap() < map.column_of(&b).unwrap());
    }

    #[test]
    fn reads_cherry_pick_trailer() {
        let id = "a3f9c21b7e0d4c5a8f1e2d3c4b5a69788796a5b4";
        let message = format!("Fix\n\n(cherry picked from commit {id})");
        assert_eq!(cherry_pick_source(&message), Some(id.parse().unwrap()));
        assert_eq!(cherry_pick_source("Fix"), None);
    }

    #[test]
    fn compact_rows_reuse_space_after_a_merge() {
        // main: a ---------- m1 ----------- m2
        //         \-- f1 --/    \-- g1 --/
        let mut h = FakeHistory::new();
        let a = h.commit("a", &[]);
        let f1 = h.commit("f1", &[a]);
        let m1 = h.commit("Merge branch 'feature/f'", &[a, f1]);
        let g1 = h.commit("g1", &[m1]);
        let m2 = h.commit("Merge branch 'feature/g'", &[m1, g1]);
        h.branch("main", m2);
        let map = h.build();

        let f = map.lane_named("feature/f").unwrap();
        let g = map.lane_named("feature/g").unwrap();
        assert_ne!(f.row, g.row);
        assert_eq!(f.compact_row, g.compact_row);
        assert_eq!(map.lane_named("main").unwrap().compact_row, 0);
    }

    #[test]
    fn overlapping_lanes_never_share_a_compact_row() {
        let mut h = FakeHistory::new();
        let a = h.commit("a", &[]);
        let f1 = h.commit("f1", &[a]);
        let g1 = h.commit("g1", &[a]);
        h.branch("main", a);
        h.branch("feature/f", f1);
        h.branch("feature/g", g1);
        let map = h.build();
        let f = map.lane_named("feature/f").unwrap();
        let g = map.lane_named("feature/g").unwrap();
        assert_ne!(f.compact_row, g.compact_row);
    }

    #[test]
    fn trunk_is_always_color_zero() {
        let mut h = FakeHistory::new();
        let a = h.commit("a", &[]);
        let f = h.commit("f", &[a]);
        h.branch("feature/a", f);
        h.branch("main", a);
        let map = h.build();
        assert_eq!(map.lane_named("main").unwrap().color, 0);
        assert_ne!(map.lane_named("feature/a").unwrap().color, 0);
    }
}
