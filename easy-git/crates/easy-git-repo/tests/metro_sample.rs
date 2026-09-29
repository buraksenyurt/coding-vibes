//! The whole pipeline on the sample repository: gix -> core -> metro map.

use easy_git_core::{LaneLabel, MapOptions, TransitionKind, build_metro_map, describe};
use easy_git_repo::GixSource;
use easy_git_repo::fixture::build_sample_repo;

fn sample_map() -> easy_git_core::MetroMap {
    let root = tempfile::tempdir().unwrap();
    let sample = build_sample_repo(root.path()).unwrap();
    let source = GixSource::open(&sample.work_dir).unwrap();
    build_metro_map(&source, &MapOptions::default()).unwrap()
}

#[test]
fn matches_the_plan() {
    let map = sample_map();

    let named = map
        .lanes
        .iter()
        .filter(|l| matches!(l.label, LaneLabel::Named(_)))
        .count();
    assert_eq!(named, 6);
    let inferred: Vec<_> = map
        .lanes
        .iter()
        .filter(|l| matches!(l.label, LaneLabel::Inferred(_)))
        .collect();
    assert_eq!(inferred.len(), 1);
    assert_eq!(inferred[0].label.text(), "feature/old-search");

    let count = |kind| map.transitions.iter().filter(|t| t.kind == kind).count();
    assert_eq!(count(TransitionKind::Merge), 5);
    assert_eq!(count(TransitionKind::CherryPick), 1);
    assert!(!map.truncated);
}

#[test]
fn fast_forwarded_branch_leaves_no_lane() {
    let map = sample_map();
    assert!(map.lanes.iter().all(|l| !l.label.text().contains("typo")));
    let typo = map
        .commits
        .iter()
        .position(|c| c.summary == "Fix typo in docs")
        .unwrap();
    assert_eq!(map.lane(map.lane_of[typo]).label.text(), "develop");
}

#[test]
fn snapshot() {
    insta::assert_snapshot!(describe(&sample_map()));
}

#[test]
fn branch_stats_tell_the_story() {
    let map = sample_map();
    let stats = easy_git_core::branch_stats(&map, 1_767_900_000, 30);
    let get = |name: &str| stats.iter().find(|s| s.name == name).unwrap();

    let main = get("main");
    let upstream = main.upstream.as_ref().unwrap();
    assert_eq!(
        (upstream.name.as_str(), upstream.ahead, upstream.behind),
        ("origin/main", 1, 0)
    );

    assert!(get("feature/login").merged);
    assert!(get("hotfix/crash").merged);
    assert!(get("release/1.0").merged);

    let payments = get("feature/payments");
    assert!(!payments.merged);
    assert_eq!(payments.commit_count, 2);
    assert_eq!(payments.authors, ["Ayla Kaya"]);
    // release/1.0 was cut from develop's tip and merged into main, so main
    // already contains all of develop; develop only lacks main's own commits.
    let develop = get("develop");
    assert_eq!(develop.ahead, 0);
    assert!(develop.merged);
    assert!(develop.behind > 0);
}
