//! A plain-text drawing of a metro map. Used for snapshot tests and handy
//! when debugging the layout without a UI.

use std::fmt::Write;

use crate::{LaneLabel, MetroMap, TransitionKind};

/// One line per lane (stations as `o`, merges as `@`), then the transitions.
pub fn describe(map: &MetroMap) -> String {
    let width = map
        .lanes
        .iter()
        .map(|l| label(&l.label).len())
        .max()
        .unwrap_or(0);
    let mut out = String::new();

    let mut lanes: Vec<_> = map.lanes.iter().collect();
    lanes.sort_by_key(|l| l.row);
    for lane in lanes {
        let mut track: Vec<char> = vec![' '; map.commits.len()];
        let start = lane.fork_column.unwrap_or(lane.first_column);
        for cell in track.iter_mut().take(lane.last_column + 1).skip(start) {
            *cell = '-';
        }
        for (column, _) in map
            .lane_of
            .iter()
            .enumerate()
            .filter(|(_, l)| **l == lane.id)
        {
            track[column] = if map.commits[column].is_merge() {
                '@'
            } else {
                'o'
            };
        }
        if let Some(fork) = lane.fork_column {
            track[fork] = '<';
        }
        let track: String = track.into_iter().collect();
        writeln!(out, "{:width$} |{}|", label(&lane.label), track.trim_end()).unwrap();
    }

    writeln!(out).unwrap();
    let rows = map
        .lanes
        .iter()
        .map(|l| l.compact_row)
        .max()
        .map_or(0, |r| r + 1);
    for row in 0..rows {
        let names: Vec<_> = map
            .lanes
            .iter()
            .filter(|l| l.compact_row == row)
            .map(|l| label(&l.label))
            .collect();
        writeln!(out, "compact row {row}: {}", names.join(" · ")).unwrap();
    }

    writeln!(out).unwrap();
    for t in &map.transitions {
        let kind = match t.kind {
            TransitionKind::Fork => "fork",
            TransitionKind::Merge => "merge",
            TransitionKind::CherryPick => "cherry-pick",
        };
        writeln!(
            out,
            "{kind:11} {} @{} -> {} @{}",
            label(&map.lanes[t.from_lane].label),
            t.from_column,
            label(&map.lanes[t.to_lane].label),
            t.to_column
        )
        .unwrap();
    }

    writeln!(out).unwrap();
    for (column, refs) in &map.refs_at {
        let names: Vec<_> = refs.iter().map(|r| r.name.as_str()).collect();
        writeln!(
            out,
            "@{column:<3} {:40} {}",
            map.commits[*column].summary,
            names.join(", ")
        )
        .unwrap();
    }
    out
}

fn label(label: &LaneLabel) -> String {
    match label {
        LaneLabel::Named(n) => n.clone(),
        LaneLabel::Inferred(n) => format!("{n} (deleted)"),
        LaneLabel::Anonymous => "(anonymous)".into(),
    }
}
