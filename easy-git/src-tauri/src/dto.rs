//! Shapes that cross the IPC boundary. The core keeps `[u8; 20]` ids and rich
//! enums; the front end gets hex strings, plain numbers and string tags.
//!
//! `ts-rs` generates a matching TypeScript file for every type marked
//! `#[ts(export)]` when `cargo test` runs, so the two sides never drift.

use easy_git_core::{GitRef, Head, LaneLabel, MetroMap, RefKind, TransitionKind};
use serde::{Deserialize, Serialize};
use ts_rs::TS;

#[derive(Debug, Clone, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct RepoSummary {
    pub name: String,
    pub path: String,
    pub head: HeadDto,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct RecentRepository {
    pub name: String,
    pub path: String,
}

#[derive(Debug, Clone, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct MetroMapDto {
    pub commits: Vec<CommitDto>,
    pub lanes: Vec<LaneDto>,
    pub transitions: Vec<TransitionDto>,
    pub refs: Vec<RefDto>,
    pub head: HeadDto,
    /// Older history exists but was cut off by the limit.
    pub truncated: bool,
}

#[derive(Debug, Clone, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct CommitDto {
    pub id: String,
    pub short_id: String,
    pub summary: String,
    pub author_name: String,
    pub author_email: String,
    /// Committer time, seconds since the Unix epoch.
    #[ts(type = "number")]
    pub time: i64,
    pub parents: Vec<String>,
    pub lane: usize,
    pub is_merge: bool,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub enum LabelKind {
    Named,
    Inferred,
    Anonymous,
}

#[derive(Debug, Clone, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct LaneDto {
    pub id: usize,
    pub label: String,
    pub label_kind: LabelKind,
    pub row: usize,
    pub color: u8,
    pub refs: Vec<String>,
    pub first_column: usize,
    pub last_column: usize,
    pub fork_column: Option<usize>,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub enum TransitionKindDto {
    Fork,
    Merge,
    CherryPick,
}

#[derive(Debug, Clone, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct TransitionDto {
    pub kind: TransitionKindDto,
    pub from_lane: usize,
    pub from_column: usize,
    pub to_lane: usize,
    pub to_column: usize,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub enum RefKindDto {
    Local,
    Remote,
    Tag,
}

#[derive(Debug, Clone, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct RefDto {
    pub name: String,
    pub kind: RefKindDto,
    pub column: usize,
}

#[derive(Debug, Clone, Serialize, TS)]
#[serde(tag = "kind", rename_all = "camelCase")]
#[ts(export)]
pub enum HeadDto {
    Branch { name: String, target: String },
    Detached { target: String },
    Unborn { name: String },
}

impl From<&Head> for HeadDto {
    fn from(head: &Head) -> Self {
        match head {
            Head::Branch { name, target } => HeadDto::Branch {
                name: name.clone(),
                target: target.to_hex(),
            },
            Head::Detached(id) => HeadDto::Detached {
                target: id.to_hex(),
            },
            Head::Unborn { name } => HeadDto::Unborn { name: name.clone() },
        }
    }
}

fn ref_kind(kind: &RefKind) -> RefKindDto {
    match kind {
        RefKind::LocalBranch => RefKindDto::Local,
        RefKind::RemoteBranch { .. } => RefKindDto::Remote,
        RefKind::Tag => RefKindDto::Tag,
    }
}

fn ref_dto(r: &GitRef, column: usize) -> RefDto {
    RefDto {
        name: r.name.clone(),
        kind: ref_kind(&r.kind),
        column,
    }
}

impl From<&MetroMap> for MetroMapDto {
    fn from(map: &MetroMap) -> Self {
        let commits = map
            .commits
            .iter()
            .zip(&map.lane_of)
            .map(|(c, &lane)| CommitDto {
                id: c.id.to_hex(),
                short_id: c.id.short(),
                summary: c.summary.clone(),
                author_name: c.author.name.clone(),
                author_email: c.author.email.clone(),
                time: c.committer.time.seconds,
                parents: c.parents.iter().map(|p| p.to_hex()).collect(),
                lane,
                is_merge: c.is_merge(),
            })
            .collect();

        let lanes = map
            .lanes
            .iter()
            .map(|l| LaneDto {
                id: l.id,
                label: l.label.text().to_owned(),
                label_kind: match l.label {
                    LaneLabel::Named(_) => LabelKind::Named,
                    LaneLabel::Inferred(_) => LabelKind::Inferred,
                    LaneLabel::Anonymous => LabelKind::Anonymous,
                },
                row: l.row,
                color: l.color,
                refs: l.refs.clone(),
                first_column: l.first_column,
                last_column: l.last_column,
                fork_column: l.fork_column,
            })
            .collect();

        let transitions = map
            .transitions
            .iter()
            .map(|t| TransitionDto {
                kind: match t.kind {
                    TransitionKind::Fork => TransitionKindDto::Fork,
                    TransitionKind::Merge => TransitionKindDto::Merge,
                    TransitionKind::CherryPick => TransitionKindDto::CherryPick,
                },
                from_lane: t.from_lane,
                from_column: t.from_column,
                to_lane: t.to_lane,
                to_column: t.to_column,
            })
            .collect();

        let refs = map
            .refs_at
            .iter()
            .flat_map(|(&column, refs)| refs.iter().map(move |r| ref_dto(r, column)))
            .collect();

        MetroMapDto {
            commits,
            lanes,
            transitions,
            refs,
            head: HeadDto::from(&map.head),
            truncated: map.truncated,
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use easy_git_core::build_metro_map;
    use easy_git_repo::GixSource;
    use easy_git_repo::fixture::build_sample_repo;

    fn sample_dto() -> MetroMapDto {
        let root = tempfile::tempdir().unwrap();
        let sample = build_sample_repo(root.path()).unwrap();
        let source = GixSource::open(&sample.work_dir).unwrap();
        MetroMapDto::from(&build_metro_map(&source, &Default::default()).unwrap())
    }

    #[test]
    fn serialises_with_camel_case_and_string_tags() {
        let json = serde_json::to_value(sample_dto()).unwrap();
        assert_eq!(json["head"]["kind"], "branch");
        assert_eq!(json["head"]["name"], "main");
        assert!(json["lanes"][0]["firstColumn"].is_number());
        assert!(
            json["transitions"]
                .as_array()
                .unwrap()
                .iter()
                .any(|t| t["kind"] == "cherryPick")
        );
        assert_eq!(json["commits"][0]["shortId"].as_str().unwrap().len(), 7);
    }

    /// Writes the sample map for the browser-only demo mode of the front end.
    /// `cargo test -p easy-git -- --ignored export_demo_map`
    #[test]
    #[ignore]
    fn export_demo_map() {
        let path = std::path::Path::new(env!("CARGO_MANIFEST_DIR"))
            .join("../src/lib/demo/sample-map.json");
        std::fs::create_dir_all(path.parent().unwrap()).unwrap();
        std::fs::write(&path, serde_json::to_string_pretty(&sample_dto()).unwrap()).unwrap();
    }
}
