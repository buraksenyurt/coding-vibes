//! A hand-written `HistorySource` for unit tests: build a graph in a few
//! lines, no repository on disk.

use crate::{
    Commit, CommitId, GitRef, Head, HistorySource, MapOptions, MetroMap, RefKind, Signature,
    SourceError, Timestamp, build_metro_map,
};

pub struct FakeHistory {
    commits: Vec<Commit>,
    refs: Vec<GitRef>,
}

impl FakeHistory {
    pub fn new() -> Self {
        Self {
            commits: Vec::new(),
            refs: Vec::new(),
        }
    }

    /// Adds a commit one minute after the previous one.
    pub fn commit(&mut self, summary: &str, parents: &[CommitId]) -> CommitId {
        self.commit_at(summary, parents, 0)
    }

    /// Like `commit`, with the clock shifted by `skew` seconds.
    pub fn commit_at(&mut self, summary: &str, parents: &[CommitId], skew: i64) -> CommitId {
        let n = self.commits.len() as u8 + 1;
        let id = CommitId::from_bytes([n; 20]);
        let time = Timestamp {
            seconds: 1_700_000_000 + i64::from(n) * 60 + skew,
            offset_minutes: 0,
        };
        let who = Signature {
            name: "Test".into(),
            email: "test@example.com".into(),
            time,
        };
        self.commits.push(Commit {
            id,
            parents: parents.to_vec(),
            author: who.clone(),
            committer: who,
            summary: summary.to_owned(),
            message: summary.to_owned(),
        });
        id
    }

    pub fn branch(&mut self, name: &str, target: CommitId) {
        self.refs.push(GitRef {
            name: name.into(),
            kind: RefKind::LocalBranch,
            target,
        });
    }

    pub fn remote_branch(&mut self, remote: &str, name: &str, target: CommitId) {
        self.refs.push(GitRef {
            name: format!("{remote}/{name}"),
            kind: RefKind::RemoteBranch {
                remote: remote.into(),
            },
            target,
        });
    }

    pub fn build(&self) -> MetroMap {
        build_metro_map(self, &MapOptions::default()).expect("fake history never fails")
    }
}

impl HistorySource for FakeHistory {
    fn refs(&self) -> Result<Vec<GitRef>, SourceError> {
        Ok(self.refs.clone())
    }

    fn head(&self) -> Result<Head, SourceError> {
        Ok(
            match self
                .refs
                .iter()
                .find(|r| r.name == "main")
                .or(self.refs.first())
            {
                Some(r) => Head::Branch {
                    name: r.name.clone(),
                    target: r.target,
                },
                None => Head::Unborn {
                    name: "main".into(),
                },
            },
        )
    }

    fn walk(&self, _tips: &[CommitId], limit: usize) -> Result<Vec<Commit>, SourceError> {
        Ok(self.commits.iter().rev().take(limit).cloned().collect())
    }
}
