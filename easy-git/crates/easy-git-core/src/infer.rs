//! Recovering the name of a deleted branch from the merge commit that
//! swallowed it. Written by hand rather than with a regex: every format is a
//! fixed prefix followed by a name, and reading them one by one documents
//! which tools produce which messages.

/// Tries the merge message formats of git, GitHub, GitLab and Bitbucket.
pub fn branch_from_merge_message(summary: &str) -> Option<String> {
    let s = summary.trim();

    // git:       Merge branch 'feature/x'  |  Merge branch 'feature/x' into develop
    // git pull:  Merge remote-tracking branch 'origin/feature/x'
    for prefix in ["Merge branch '", "Merge remote-tracking branch '"] {
        if let Some(rest) = s.strip_prefix(prefix) {
            let name = rest.split('\'').next()?;
            return non_empty(strip_remote(name));
        }
    }

    // GitHub:    Merge pull request #42 from user/feature/x
    if let Some(rest) = s.strip_prefix("Merge pull request #") {
        let (_, from) = rest.split_once(" from ")?;
        let (_owner, branch) = from.split_once('/')?;
        return non_empty(branch.split_whitespace().next()?);
    }

    // Bitbucket: Merged in feature/x (pull request #12)
    if let Some(rest) = s.strip_prefix("Merged in ") {
        return non_empty(rest.split_whitespace().next()?);
    }

    // GitLab:    Merge branch 'feature/x' into 'main'  (covered above)
    None
}

fn strip_remote(name: &str) -> &str {
    name.strip_prefix("origin/").unwrap_or(name)
}

fn non_empty(name: &str) -> Option<String> {
    (!name.is_empty()).then(|| name.to_owned())
}

#[cfg(test)]
mod tests {
    use super::branch_from_merge_message as infer;

    #[test]
    fn understands_common_formats() {
        assert_eq!(
            infer("Merge branch 'feature/x'").as_deref(),
            Some("feature/x")
        );
        assert_eq!(
            infer("Merge branch 'feature/x' into develop").as_deref(),
            Some("feature/x")
        );
        assert_eq!(
            infer("Merge branch 'feature/x' into 'main'").as_deref(),
            Some("feature/x")
        );
        assert_eq!(
            infer("Merge remote-tracking branch 'origin/feature/x'").as_deref(),
            Some("feature/x")
        );
        assert_eq!(
            infer("Merge pull request #42 from octo/feature/x").as_deref(),
            Some("feature/x")
        );
        assert_eq!(
            infer("Merged in feature/x (pull request #12)").as_deref(),
            Some("feature/x")
        );
    }

    #[test]
    fn gives_up_politely() {
        assert_eq!(infer("Merged PR 123: Add login"), None);
        assert_eq!(infer("Fix typo"), None);
        assert_eq!(infer("Merge branch ''"), None);
    }
}
