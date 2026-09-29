/// Decides which branch claims shared history first.
///
/// The lane algorithm walks branches in this order. A commit belongs to the
/// first branch that reaches it, so the order *is* the answer to "which branch
/// was this commit made on?" when git itself cannot tell.
#[derive(Debug, Clone, PartialEq, Eq)]
pub struct BranchPriority {
    patterns: Vec<String>,
}

impl Default for BranchPriority {
    fn default() -> Self {
        Self::new([
            "main",
            "master",
            "trunk",
            "develop",
            "dev",
            "release/*",
            "hotfix/*",
            "feature/*",
        ])
    }
}

impl BranchPriority {
    /// Patterns are exact names or a prefix followed by `*` (`release/*`).
    pub fn new<I, S>(patterns: I) -> Self
    where
        I: IntoIterator<Item = S>,
        S: Into<String>,
    {
        Self {
            patterns: patterns.into_iter().map(Into::into).collect(),
        }
    }

    pub fn patterns(&self) -> &[String] {
        &self.patterns
    }

    /// Lower is more important. Unmatched names rank after every pattern.
    pub fn rank(&self, branch: &str) -> usize {
        self.patterns
            .iter()
            .position(|pattern| match pattern.strip_suffix('*') {
                Some(prefix) => branch.starts_with(prefix),
                None => branch == pattern,
            })
            .unwrap_or(self.patterns.len())
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn ranks_by_first_matching_pattern() {
        let p = BranchPriority::default();
        assert!(p.rank("main") < p.rank("develop"));
        assert!(p.rank("develop") < p.rank("release/1.0"));
        assert!(p.rank("hotfix/crash") < p.rank("feature/login"));
        assert!(p.rank("feature/login") < p.rank("spike"));
        assert_eq!(p.rank("spike"), p.rank("experiment"));
    }
}
