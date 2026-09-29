//! The "metro line story": a small repository with every branching situation
//! the plan lists (fork, merge, deleted branch, fast-forward, cherry-pick,
//! tag, remote-tracking branch).
//!
//! It is built with the `git` command line and fixed dates, so every run
//! produces exactly the same commit ids. Used by tests and by
//! `cargo run -p easy-git-repo --example make-sample-repo -- <folder>`.

use std::io;
use std::path::{Path, PathBuf};
use std::process::Command;

/// Commits in the finished repository.
pub const SAMPLE_COMMIT_COUNT: usize = 20;
/// Local branches that still exist at the end.
pub const SAMPLE_LOCAL_BRANCHES: [&str; 6] = [
    "develop",
    "feature/login",
    "feature/payments",
    "hotfix/crash",
    "main",
    "release/1.0",
];

/// 2026-01-05 09:00 in Istanbul (UTC+3).
const START: i64 = 1_767_592_800;

const AYLA: (&str, &str) = ("Ayla Kaya", "ayla@example.com");
const MERT: (&str, &str) = ("Mert Yildiz", "mert@example.com");

pub struct SampleRepo {
    /// The working repository to open in easy-git.
    pub work_dir: PathBuf,
    /// Bare repository acting as `origin`.
    pub origin_dir: PathBuf,
}

struct Builder {
    dir: PathBuf,
    config: PathBuf,
    clock: i64,
    author: (&'static str, &'static str),
}

impl Builder {
    fn git(&mut self, args: &[&str]) -> io::Result<String> {
        self.clock += 3600; // every git call happens one hour after the previous one
        let date = format!("@{} +0300", self.clock);
        let output = Command::new("git")
            .args([
                "-c",
                "init.defaultBranch=main",
                "-c",
                "commit.gpgsign=false",
                "-c",
                "tag.gpgsign=false",
            ])
            .args([
                "-c",
                "core.autocrlf=false",
                "-c",
                "advice.detachedHead=false",
            ])
            .args(args)
            .current_dir(&self.dir)
            .env("GIT_CONFIG_GLOBAL", &self.config)
            .env("GIT_CONFIG_NOSYSTEM", "1")
            .env("GIT_AUTHOR_NAME", self.author.0)
            .env("GIT_AUTHOR_EMAIL", self.author.1)
            .env("GIT_COMMITTER_NAME", self.author.0)
            .env("GIT_COMMITTER_EMAIL", self.author.1)
            .env("GIT_AUTHOR_DATE", &date)
            .env("GIT_COMMITTER_DATE", &date)
            .output()?;
        if !output.status.success() {
            return Err(io::Error::other(format!(
                "git {} failed: {}",
                args.join(" "),
                String::from_utf8_lossy(&output.stderr)
            )));
        }
        Ok(String::from_utf8_lossy(&output.stdout).trim().to_owned())
    }

    /// Each commit touches its own file, so merges never conflict.
    fn commit(&mut self, file: &str, message: &str) -> io::Result<String> {
        let path = self.dir.join(file);
        if let Some(parent) = path.parent() {
            std::fs::create_dir_all(parent)?;
        }
        std::fs::write(&path, format!("{message}\n"))?;
        self.git(&["add", file])?;
        self.git(&["commit", "-q", "-m", message])?;
        self.git(&["rev-parse", "HEAD"])
    }

    fn checkout(&mut self, branch: &str) -> io::Result<()> {
        self.git(&["checkout", "-q", branch]).map(|_| ())
    }

    fn branch_from(&mut self, new: &str, base: &str) -> io::Result<()> {
        self.git(&["checkout", "-q", "-b", new, base]).map(|_| ())
    }

    fn merge_no_ff(&mut self, into: &str, from: &str) -> io::Result<()> {
        self.checkout(into)?;
        self.git(&["merge", "-q", "--no-ff", "--no-edit", from])
            .map(|_| ())
    }
}

/// Builds the sample under `root/sample` (and `root/origin.git`).
/// `root` should be an empty folder.
pub fn build_sample_repo(root: &Path) -> io::Result<SampleRepo> {
    let work_dir = root.join("sample");
    let origin_dir = root.join("origin.git");
    std::fs::create_dir_all(&work_dir)?;
    std::fs::create_dir_all(&origin_dir)?;
    let config = root.join("gitconfig-isolated");
    std::fs::write(&config, "")?;

    let mut b = Builder {
        dir: work_dir.clone(),
        config,
        clock: START,
        author: AYLA,
    };
    b.git(&["init", "-q"])?;

    // 1. main: the trunk.
    b.commit("README.md", "Initial commit")?;
    b.commit("LICENSE", "Add license")?;

    // 2. develop forks from main.
    b.branch_from("develop", "main")?;
    b.commit("src/app.txt", "Set up project structure")?;

    // 3. feature/login: three commits, merged with --no-ff.
    b.author = MERT;
    b.branch_from("feature/login", "develop")?;
    b.commit("src/login/form.txt", "Add login form")?;
    b.commit("src/login/validation.txt", "Validate login input")?;
    b.commit("src/login/session.txt", "Keep the session after login")?;
    b.merge_no_ff("develop", "feature/login")?;

    // 4. feature/payments: stays open.
    b.author = AYLA;
    b.branch_from("feature/payments", "develop")?;
    b.commit("src/payments/model.txt", "Add payment model")?;
    let rounding = b.commit(
        "src/payments/rounding.txt",
        "Fix rounding in amount formatting",
    )?;

    // 5. hotfix/crash: from main, merged into main and develop.
    b.author = MERT;
    b.branch_from("hotfix/crash", "main")?;
    b.commit("src/config.txt", "Fix crash on empty config")?;
    b.merge_no_ff("main", "hotfix/crash")?;
    b.merge_no_ff("develop", "hotfix/crash")?;

    // 6. feature/old-search: merged, then deleted. Only the merge message remembers it.
    b.author = AYLA;
    b.branch_from("feature/old-search", "develop")?;
    b.commit("src/search/index.txt", "Add search index")?;
    b.commit("src/search/query.txt", "Add search query parser")?;
    b.merge_no_ff("develop", "feature/old-search")?;
    b.git(&["branch", "-q", "-d", "feature/old-search"])?;

    // 7. feature/typo: fast-forwarded, then deleted. Leaves no trace at all.
    b.branch_from("feature/typo", "develop")?;
    b.commit("docs/typo.txt", "Fix typo in docs")?;
    b.checkout("develop")?;
    b.git(&["merge", "-q", "--ff-only", "feature/typo"])?;
    b.git(&["branch", "-q", "-d", "feature/typo"])?;

    // 8. release/1.0: merged into main and tagged.
    b.author = MERT;
    b.branch_from("release/1.0", "develop")?;
    b.commit("VERSION", "Bump version to 1.0")?;
    b.merge_no_ff("main", "release/1.0")?;
    b.git(&["tag", "-a", "v1.0", "-m", "Release 1.0"])?;

    // 9. The rounding fix is needed on main right away.
    b.git(&["cherry-pick", "-x", &rounding])?;

    // 10. Publish to origin, then commit once more locally: main is 1 ahead.
    b.git(&[
        "init",
        "-q",
        "--bare",
        origin_dir.to_str().expect("utf-8 path"),
    ])?;
    b.git(&[
        "remote",
        "add",
        "origin",
        origin_dir.to_str().expect("utf-8 path"),
    ])?;
    b.git(&["push", "-q", "origin", "main", "develop"])?;
    b.git(&["fetch", "-q", "origin"])?;
    b.commit("CHANGELOG.md", "Update changelog")?;

    Ok(SampleRepo {
        work_dir,
        origin_dir,
    })
}
