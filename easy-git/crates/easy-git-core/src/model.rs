use crate::CommitId;

/// Seconds since the Unix epoch plus the author's UTC offset, as git stores it.
#[derive(Debug, Clone, Copy, PartialEq, Eq, PartialOrd, Ord, Hash)]
pub struct Timestamp {
    pub seconds: i64,
    pub offset_minutes: i32,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Signature {
    pub name: String,
    pub email: String,
    pub time: Timestamp,
}

/// One commit as read from the object database.
///
/// Note what is missing: a commit does not know which branch it was made on.
/// Git never stores that. The lane algorithm infers it later.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct Commit {
    pub id: CommitId,
    /// 0 parents: root commit. 1: ordinary commit. 2 or more: merge.
    pub parents: Vec<CommitId>,
    pub author: Signature,
    pub committer: Signature,
    /// First line of the message.
    pub summary: String,
    /// Full message including the summary.
    pub message: String,
}

impl Commit {
    pub fn is_merge(&self) -> bool {
        self.parents.len() > 1
    }

    pub fn is_root(&self) -> bool {
        self.parents.is_empty()
    }
}

#[derive(Debug, Clone, PartialEq, Eq, Hash, PartialOrd, Ord)]
pub enum RefKind {
    LocalBranch,
    RemoteBranch { remote: String },
    Tag,
}

/// A named pointer into history, already peeled to the commit it points at.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct GitRef {
    /// Short name: `main`, `origin/main`, `v1.0`.
    pub name: String,
    pub kind: RefKind,
    pub target: CommitId,
}

impl GitRef {
    pub fn is_branch(&self) -> bool {
        matches!(
            self.kind,
            RefKind::LocalBranch | RefKind::RemoteBranch { .. }
        )
    }

    /// For `origin/feature/x` returns `feature/x`; for local branches the name itself.
    pub fn branch_name(&self) -> &str {
        match &self.kind {
            RefKind::RemoteBranch { remote } => self
                .name
                .strip_prefix(remote.as_str())
                .and_then(|rest| rest.strip_prefix('/'))
                .unwrap_or(&self.name),
            _ => &self.name,
        }
    }
}

/// Where `HEAD` points.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum Head {
    /// On a branch, e.g. `main`.
    Branch { name: String, target: CommitId },
    /// Checked out a commit directly.
    Detached(CommitId),
    /// Fresh repository with no commits yet.
    Unborn { name: String },
}

impl Head {
    pub fn target(&self) -> Option<CommitId> {
        match self {
            Head::Branch { target, .. } => Some(*target),
            Head::Detached(id) => Some(*id),
            Head::Unborn { .. } => None,
        }
    }
}
