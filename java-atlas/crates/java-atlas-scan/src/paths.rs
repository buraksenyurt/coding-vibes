//! Small path helpers.
//!
//! We avoid `fs::canonicalize`: on Windows it returns `\\?\C:\...` paths and
//! it resolves symlinks, both of which make relative paths surprising.
//! A purely *lexical* normalisation is enough for build files.

use std::path::{Component, Path, PathBuf};

/// Removes `.` and folds `..` without touching the disk.
pub fn normalize(path: &Path) -> PathBuf {
    let mut out = PathBuf::new();
    for component in path.components() {
        match component {
            Component::CurDir => {}
            Component::ParentDir => {
                // Only pop a normal segment; never climb above a root or prefix.
                if matches!(out.components().next_back(), Some(Component::Normal(_))) {
                    out.pop();
                } else {
                    out.push("..");
                }
            }
            other => out.push(other),
        }
    }
    out
}

/// `path` relative to `root`, with `/` separators on every platform.
/// Returns the normalised path unchanged when it is outside `root`.
pub fn relative_slash(root: &Path, path: &Path) -> String {
    let root = normalize(root);
    let path = normalize(path);
    let rel = path.strip_prefix(&root).unwrap_or(&path);
    rel.components()
        .map(|c| c.as_os_str().to_string_lossy())
        .collect::<Vec<_>>()
        .join("/")
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn folds_parent_segments() {
        assert_eq!(
            normalize(Path::new("a/b/../c/./pom.xml")),
            PathBuf::from("a/c/pom.xml")
        );
    }

    #[test]
    fn keeps_leading_parent_segments() {
        assert_eq!(normalize(Path::new("../x")), PathBuf::from("../x"));
    }

    #[test]
    fn relative_paths_use_forward_slashes() {
        let root = Path::new("work/bank");
        assert_eq!(
            relative_slash(root, &root.join("bank-api").join("pom.xml")),
            "bank-api/pom.xml"
        );
        assert_eq!(relative_slash(root, root), "");
    }
}
