use std::hint::black_box;

use criterion::{BenchmarkId, Criterion, criterion_group, criterion_main};
use easy_git_core::{
    BranchPriority, Commit, CommitId, GitRef, Head, RefKind, Signature, Timestamp, describe, layout,
};

fn commit(sequence: u64, parent: Option<CommitId>) -> Commit {
    let mut bytes = [0; 20];
    bytes[..8].copy_from_slice(&sequence.to_be_bytes());
    let id = CommitId::from_bytes(bytes);
    let time = Timestamp {
        seconds: sequence as i64,
        offset_minutes: 0,
    };
    let signature = Signature {
        name: "Benchmark".into(),
        email: "benchmark@example.com".into(),
        time,
    };
    Commit {
        id,
        parents: parent.into_iter().collect(),
        author: signature.clone(),
        committer: signature,
        summary: format!("commit {sequence}"),
        message: format!("commit {sequence}"),
    }
}

fn map_with_forked_branches(lane_count: usize, commits_per_lane: usize) -> easy_git_core::MetroMap {
    let mut commits = Vec::with_capacity(lane_count * commits_per_lane);
    let mut refs = Vec::with_capacity(lane_count);
    let mut sequence = 0u64;
    let mut main_tip = None;

    for _ in 0..commits_per_lane {
        sequence += 1;
        let next = commit(sequence, main_tip);
        main_tip = Some(next.id);
        commits.push(next);
    }

    let main_tip = main_tip.expect("each benchmark lane has at least one commit");
    refs.push(GitRef {
        name: "main".into(),
        kind: RefKind::LocalBranch,
        target: main_tip,
    });

    for lane in 1..lane_count {
        let mut tip = main_tip;
        for _ in 0..commits_per_lane {
            sequence += 1;
            let next = commit(sequence, Some(tip));
            tip = next.id;
            commits.push(next);
        }
        refs.push(GitRef {
            name: format!("feature/{lane}"),
            kind: RefKind::LocalBranch,
            target: tip,
        });
    }

    layout(
        commits,
        refs,
        Head::Branch {
            name: "main".into(),
            target: main_tip,
        },
        &BranchPriority::default(),
        false,
    )
}

fn benchmark_describe(c: &mut Criterion) {
    let scenarios = [(1, 1_000), (16, 256), (64, 256), (128, 64), (256, 16),(256,1000)];
    let mut group = c.benchmark_group("describe");

    for (lane_count, commits_per_lane) in scenarios {
        let map = map_with_forked_branches(lane_count, commits_per_lane);
        let label = format!("{lane_count} lanes x {commits_per_lane} commits");
        group.bench_with_input(BenchmarkId::new("map", label), &map, |b, map| {
            b.iter(|| black_box(describe(black_box(map))));
        });
    }

    group.finish();
}

criterion_group!(benches, benchmark_describe);
criterion_main!(benches);
