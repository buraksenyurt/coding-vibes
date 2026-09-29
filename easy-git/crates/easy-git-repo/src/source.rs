use std::path::Path;

use easy_git_core::{Commit, CommitId, GitRef, Head, HistorySource, RefKind, SourceError};
use gix::bstr::ByteSlice;

use crate::RepoError;
use crate::convert::{commit_id, object_id, signature};

/// Reads a repository on disk through gix. Read-only by construction:
/// nothing in this type writes to the repository.
pub struct GixSource {
    repo: gix::ThreadSafeRepository,
}

impl GixSource {
    /// Opens the repository containing `path`. Selecting a sub folder of a
    /// working tree works too, just like running `git` from there.
    pub fn open(path: impl AsRef<Path>) -> Result<Self, RepoError> {
        let path = path.as_ref();
        let repo = gix::discover(path)
            .map_err(|_| RepoError::NotARepository(path.display().to_string()))?;
        Ok(Self {
            repo: repo.into_sync(),
        })
    }

    /// Working tree root, or the git dir for a bare repository.
    pub fn root(&self) -> &Path {
        self.repo.work_dir().unwrap_or_else(|| self.repo.git_dir())
    }

    /// Folder name shown in the title bar.
    pub fn name(&self) -> String {
        self.root()
            .file_name()
            .map(|n| n.to_string_lossy().into_owned())
            .unwrap_or_else(|| self.root().display().to_string())
    }

    fn local(&self) -> gix::Repository {
        self.repo.to_thread_local()
    }

    fn read_refs(&self) -> Result<Vec<GitRef>, RepoError> {
        let repo = self.local();
        let platform = repo
            .references()
            .map_err(|e| RepoError::References(e.to_string()))?;
        let iter = platform
            .all()
            .map_err(|e| RepoError::References(e.to_string()))?;

        let mut refs = Vec::new();
        for reference in iter {
            let mut reference = reference.map_err(|e| RepoError::References(e.to_string()))?;
            let full = reference.name().as_bstr().to_str_lossy().into_owned();
            let Some((kind, name)) = classify(&full) else {
                continue;
            };

            // Annotated tags point at a tag object; peel until we reach the commit.
            let Ok(id) = reference.peel_to_id() else {
                continue;
            };
            let Ok(object) = id.object() else { continue };
            let Ok(commit) = object.peel_to_commit() else {
                continue;
            };

            refs.push(GitRef {
                name,
                kind,
                target: commit_id(&commit.id),
            });
        }
        refs.sort_by(|a, b| (&a.kind, &a.name).cmp(&(&b.kind, &b.name)));
        Ok(refs)
    }

    fn read_head(&self) -> Result<Head, RepoError> {
        let repo = self.local();
        let head = repo
            .head()
            .map_err(|e| RepoError::References(e.to_string()))?;
        let short = head
            .referent_name()
            .map(|n| n.shorten().to_str_lossy().into_owned());
        if head.is_unborn() {
            return Ok(Head::Unborn {
                name: short.unwrap_or_default(),
            });
        }
        let id = head.id().map(|id| commit_id(&id));
        Ok(match (short, id) {
            (Some(name), Some(target)) => Head::Branch { name, target },
            (None, Some(target)) => Head::Detached(target),
            (name, None) => Head::Unborn {
                name: name.unwrap_or_default(),
            },
        })
    }

    fn read_walk(&self, tips: &[CommitId], limit: usize) -> Result<Vec<Commit>, RepoError> {
        let repo = self.local();
        let walk = repo
            .rev_walk(tips.iter().copied().map(object_id))
            .sorting(gix::revision::walk::Sorting::ByCommitTime(
                Default::default(),
            ))
            .all()
            .map_err(|e| RepoError::Walk(e.to_string()))?;

        let mut commits = Vec::with_capacity(limit.min(4096));
        for info in walk.take(limit) {
            let info = info.map_err(|e| RepoError::Walk(e.to_string()))?;
            let decode_err = |e: &dyn std::fmt::Display| RepoError::Decode {
                id: info.id.to_string(),
                detail: e.to_string(),
            };
            let object = info.object().map_err(|e| decode_err(&e))?;
            let decoded = object.decode().map_err(|e| decode_err(&e))?;

            let author = decoded.author().map_err(|e| decode_err(&e))?;
            let committer = decoded.committer().map_err(|e| decode_err(&e))?;
            let message = decoded.message.to_str_lossy().trim_end().to_owned();
            let summary = decoded.message_summary().to_str_lossy().into_owned();
            commits.push(Commit {
                id: commit_id(&info.id),
                parents: decoded.parents().map(|p| commit_id(&p)).collect(),
                author: signature(author),
                committer: signature(committer),
                summary,
                message,
            });
        }
        Ok(commits)
    }
}

/// Maps a full ref name to what we show, or `None` for refs we ignore
/// (`refs/stash`, `refs/remotes/origin/HEAD`, notes, ...).
fn classify(full: &str) -> Option<(RefKind, String)> {
    if let Some(name) = full.strip_prefix("refs/heads/") {
        return Some((RefKind::LocalBranch, name.to_owned()));
    }
    if let Some(name) = full.strip_prefix("refs/tags/") {
        return Some((RefKind::Tag, name.to_owned()));
    }
    if let Some(rest) = full.strip_prefix("refs/remotes/") {
        let (remote, branch) = rest.split_once('/')?;
        if branch == "HEAD" {
            return None;
        }
        return Some((
            RefKind::RemoteBranch {
                remote: remote.to_owned(),
            },
            rest.to_owned(),
        ));
    }
    None
}

impl HistorySource for GixSource {
    fn refs(&self) -> Result<Vec<GitRef>, SourceError> {
        Ok(self.read_refs()?)
    }

    fn head(&self) -> Result<Head, SourceError> {
        Ok(self.read_head()?)
    }

    fn walk(&self, tips: &[CommitId], limit: usize) -> Result<Vec<Commit>, SourceError> {
        Ok(self.read_walk(tips, limit)?)
    }
}

#[cfg(test)]
mod tests {
    use super::classify;
    use easy_git_core::RefKind;

    #[test]
    fn classifies_ref_names() {
        assert_eq!(
            classify("refs/heads/feature/login"),
            Some((RefKind::LocalBranch, "feature/login".into()))
        );
        assert_eq!(
            classify("refs/tags/v1.0"),
            Some((RefKind::Tag, "v1.0".into()))
        );
        assert_eq!(
            classify("refs/remotes/origin/main"),
            Some((
                RefKind::RemoteBranch {
                    remote: "origin".into()
                },
                "origin/main".into()
            ))
        );
        assert_eq!(classify("refs/remotes/origin/HEAD"), None);
        assert_eq!(classify("refs/stash"), None);
    }
}
