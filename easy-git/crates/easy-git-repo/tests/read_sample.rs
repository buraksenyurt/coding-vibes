//! Reads the fixture repository through GixSource and checks what comes back.

use std::collections::HashSet;

use easy_git_core::{Head, HistorySource, RefKind};
use easy_git_repo::GixSource;
use easy_git_repo::fixture::{SAMPLE_COMMIT_COUNT, SAMPLE_LOCAL_BRANCHES, build_sample_repo};

fn open_sample() -> (tempfile::TempDir, GixSource) {
    let root = tempfile::tempdir().unwrap();
    let sample = build_sample_repo(root.path()).unwrap();
    let source = GixSource::open(&sample.work_dir).unwrap();
    (root, source)
}

#[test]
fn lists_branches_remotes_and_tags() {
    let (_root, source) = open_sample();
    let refs = source.refs().unwrap();

    let local: Vec<_> = refs
        .iter()
        .filter(|r| r.kind == RefKind::LocalBranch)
        .map(|r| r.name.as_str())
        .collect();
    assert_eq!(local, SAMPLE_LOCAL_BRANCHES);

    let remote: Vec<_> = refs
        .iter()
        .filter(|r| matches!(r.kind, RefKind::RemoteBranch { .. }))
        .map(|r| r.name.as_str())
        .collect();
    assert_eq!(remote, ["origin/develop", "origin/main"]);

    let tags: Vec<_> = refs
        .iter()
        .filter(|r| r.kind == RefKind::Tag)
        .map(|r| r.name.as_str())
        .collect();
    assert_eq!(tags, ["v1.0"]);
}

#[test]
fn annotated_tag_is_peeled_to_its_commit() {
    let (_root, source) = open_sample();
    let refs = source.refs().unwrap();
    let tag = refs.iter().find(|r| r.name == "v1.0").unwrap();
    let tips: Vec<_> = refs.iter().map(|r| r.target).collect();
    let commits = source.walk(&tips, 100).unwrap();
    let tagged = commits.iter().find(|c| c.id == tag.target).unwrap();
    assert!(tagged.is_merge());
    assert_eq!(tagged.summary, "Merge branch 'release/1.0'");
}

#[test]
fn head_is_on_main() {
    let (_root, source) = open_sample();
    match source.head().unwrap() {
        Head::Branch { name, .. } => assert_eq!(name, "main"),
        other => panic!("unexpected head {other:?}"),
    }
}

#[test]
fn walks_every_commit_once() {
    let (_root, source) = open_sample();
    let tips: Vec<_> = source.refs().unwrap().iter().map(|r| r.target).collect();
    let commits = source.walk(&tips, 1000).unwrap();

    assert_eq!(commits.len(), SAMPLE_COMMIT_COUNT);
    let unique: HashSet<_> = commits.iter().map(|c| c.id).collect();
    assert_eq!(unique.len(), SAMPLE_COMMIT_COUNT);

    assert_eq!(commits.iter().filter(|c| c.is_merge()).count(), 5);
    assert_eq!(commits.iter().filter(|c| c.is_root()).count(), 1);
    // Newest first.
    assert_eq!(commits[0].summary, "Update changelog");
}

#[test]
fn respects_the_limit() {
    let (_root, source) = open_sample();
    let tips: Vec<_> = source.refs().unwrap().iter().map(|r| r.target).collect();
    assert_eq!(source.walk(&tips, 5).unwrap().len(), 5);
}

#[test]
fn keeps_cherry_pick_trailer_and_signatures() {
    let (_root, source) = open_sample();
    let tips: Vec<_> = source.refs().unwrap().iter().map(|r| r.target).collect();
    let commits = source.walk(&tips, 1000).unwrap();
    let picked: Vec<_> = commits
        .iter()
        .filter(|c| c.message.contains("(cherry picked from commit "))
        .collect();
    assert_eq!(picked.len(), 1);
    assert_eq!(picked[0].summary, "Fix rounding in amount formatting");
    assert_eq!(picked[0].author.name, "Ayla Kaya");
    assert_eq!(picked[0].author.time.offset_minutes, 180);
}

#[test]
fn opening_a_plain_folder_fails_clearly() {
    let dir = tempfile::tempdir().unwrap();
    let err = GixSource::open(dir.path()).err().unwrap();
    assert!(err.to_string().contains("not inside a git repository"));
}
