//! An opened project: the atlas plus everything precomputed to answer views
//! quickly, and the conversions from core types to DTOs.

use std::collections::{HashMap, HashSet};
use std::path::Path;
use std::sync::Mutex;
use std::time::{Duration, Instant};

use java_atlas_core::Scale;
use java_atlas_core::atlas::{
    Atlas, Edge, FindingKind, Level, MatchMethod, NodeId, NodeKind, ViewFilters, ViolationKind,
};
use java_atlas_core::facts::{BuildSystem, TypeKind};
use java_atlas_scan::{ScanOptions, jars, read_build, read_sources};

use crate::dto::*;
use crate::error::{AppError, ErrorKind};

/// How often parsing progress is sent at most. One message per file would
/// flood the IPC channel on a large project (Guava: 1 556 files).
const PROGRESS_INTERVAL: Duration = Duration::from_millis(40);

pub struct OpenProject {
    pub atlas: Atlas,
    pub summary: ProjectSummary,
    keys: HashMap<String, NodeId>,
    sizes: Vec<u32>,
    /// Packages in a cycle, mapped to the cycle's index.
    cycle_of: HashMap<NodeId, usize>,
    /// Modules that contain at least one package in a cycle.
    modules_in_cycle: HashSet<NodeId>,
}

/// Reads, parses and analyses the project at `root`, reporting progress.
/// Runs on a worker thread (see `commands::open_project`).
pub fn scan(root: &Path, report: &(dyn Fn(ScanEvent) + Sync)) -> Result<OpenProject, AppError> {
    if !root.is_dir() {
        return Err(AppError::new(
            ErrorKind::NotFound,
            format!("'{}' is not a folder", root.display()),
        ));
    }
    let started = Instant::now();

    report(ScanEvent::ReadingBuild);
    let mut facts = read_build(root)?;

    let last_sent = Mutex::new(None::<Instant>);
    read_sources(root, &mut facts, &|p| {
        let mut last = last_sent.lock().expect("progress lock poisoned");
        let due = last.is_none_or(|t| t.elapsed() >= PROGRESS_INTERVAL);
        if due || p.done == p.total {
            *last = Some(Instant::now());
            report(ScanEvent::ParsingSources {
                done: p.done as u32,
                total: p.total as u32,
            });
        }
    })?;

    report(ScanEvent::IndexingLibraries);
    jars::index_libraries(&mut facts, &ScanOptions::with_default_caches());
    let scan_ms = started.elapsed().as_millis() as u32;

    report(ScanEvent::Analysing);
    let analysis_started = Instant::now();
    let atlas = Atlas::build(&facts);
    let analysis_ms = analysis_started.elapsed().as_millis() as u32;

    let build_system = match facts.build_system {
        BuildSystem::Maven => "Maven",
        BuildSystem::Gradle => "Gradle",
        BuildSystem::Plain => "Plain",
    };
    let count =
        |pred: fn(&NodeKind) -> bool| atlas.nodes.iter().filter(|n| pred(&n.kind)).count() as u32;
    let summary = ProjectSummary {
        name: atlas.name.clone(),
        root: root.display().to_string(),
        build_system: build_system.to_owned(),
        modules: count(|k| matches!(k, NodeKind::Module { .. })),
        packages: count(|k| matches!(k, NodeKind::Package { .. })),
        types: count(|k| matches!(k, NodeKind::Type { .. })),
        files: facts.file_count() as u32,
        libraries: count(|k| matches!(k, NodeKind::Library { .. })),
        unknown_packages: count(|k| matches!(k, NodeKind::Unknown)),
        indexed_jars: facts.libraries.len() as u32,
        scan_ms,
        analysis_ms,
        cycles: Vec::new(),
        violations: Vec::new(),
        findings: Vec::new(),
        warnings: facts
            .warnings
            .iter()
            .map(|w| WarningDto {
                file: w.file.clone(),
                message: w.message.clone(),
            })
            .collect(),
    };
    Ok(OpenProject::new(atlas, summary))
}

fn type_kind(kind: TypeKind) -> TypeKindDto {
    match kind {
        TypeKind::Class => TypeKindDto::Class,
        TypeKind::Interface => TypeKindDto::Interface,
        TypeKind::Enum => TypeKindDto::Enum,
        TypeKind::Record => TypeKindDto::Record,
        TypeKind::Annotation => TypeKindDto::Annotation,
    }
}

fn method_name(m: MatchMethod) -> &'static str {
    match m {
        MatchMethod::Jar => "jar",
        MatchMethod::Name => "name",
    }
}

pub fn scale(s: ScaleDto) -> Scale {
    match s {
        ScaleDto::Country => Scale::Country,
        ScaleDto::Region => Scale::Region,
        ScaleDto::City => Scale::City,
    }
}

impl OpenProject {
    fn new(atlas: Atlas, mut summary: ProjectSummary) -> Self {
        let keys = atlas.key_index();
        let sizes = atlas.sizes();
        let mut cycle_of = HashMap::new();
        let mut modules_in_cycle = HashSet::new();
        for (i, c) in atlas.cycles.iter().enumerate() {
            for p in &c.packages {
                cycle_of.insert(*p, i);
                modules_in_cycle.extend(atlas.module_of(*p));
            }
        }
        let mut project = OpenProject {
            atlas,
            summary: summary.clone(),
            keys,
            sizes,
            cycle_of,
            modules_in_cycle,
        };

        summary.cycles = project.cycles();
        summary.violations = project.violations();
        summary.findings = project.findings();
        project.summary = summary;
        project
    }

    pub fn node_id(&self, key: &str) -> Result<NodeId, AppError> {
        self.keys.get(key).copied().ok_or_else(|| {
            AppError::new(ErrorKind::UnknownNode, format!("no node with key '{key}'"))
        })
    }

    fn node_ref(&self, id: NodeId) -> NodeRef {
        let n = self.atlas.node(id);
        let name = match n.kind {
            // A package name alone is ambiguous across modules but reads best.
            NodeKind::Library { .. } => n.fqn.clone(),
            _ => n.name.clone(),
        };
        NodeRef {
            key: self.atlas.key(id),
            name,
        }
    }

    pub fn node_dto(&self, id: NodeId) -> NodeDto {
        let n = self.atlas.node(id);
        let (kind, type_kind_dto, exported, jpms, version, matched_by) = match &n.kind {
            NodeKind::Module { jpms, .. } => (
                NodeKindDto::Module,
                None,
                None,
                jpms.as_ref().map(|j| j.name.clone()),
                None,
                None,
            ),
            NodeKind::Package { exported } => {
                (NodeKindDto::Package, None, *exported, None, None, None)
            }
            NodeKind::Type { kind, .. } => (
                NodeKindDto::Type,
                Some(type_kind(*kind)),
                None,
                None,
                None,
                None,
            ),
            NodeKind::Library {
                version, packages, ..
            } => {
                let methods: HashSet<MatchMethod> = packages.iter().map(|(_, m)| *m).collect();
                let matched = match methods.len() {
                    0 => None,
                    1 => methods.iter().next().map(|m| method_name(*m).to_owned()),
                    _ => Some("mixed".to_owned()),
                };
                (
                    NodeKindDto::Library,
                    None,
                    None,
                    None,
                    version.clone(),
                    matched,
                )
            }
            NodeKind::Jdk => (NodeKindDto::Jdk, None, None, None, None, None),
            NodeKind::Unknown => (NodeKindDto::Unknown, None, None, None, None, None),
        };
        NodeDto {
            key: self.atlas.key(id),
            kind,
            name: n.name.clone(),
            fqn: n.fqn.clone(),
            parent: n.parent.map(|p| self.atlas.key(p)),
            is_test: n.is_test,
            size: self.sizes[id.index()],
            type_kind: type_kind_dto,
            exported,
            jpms,
            in_cycle: self.cycle_of.contains_key(&id) || self.modules_in_cycle.contains(&id),
            metrics: self.atlas.metrics(id).map(|m| MetricsDto {
                ca: m.ca,
                ce: m.ce,
                instability: m.instability(),
            }),
            version,
            matched_by,
        }
    }

    // -- report ---------------------------------------------------------------

    fn cycles(&self) -> Vec<CycleDto> {
        self.atlas
            .cycles
            .iter()
            .map(|c| CycleDto {
                packages: c.packages.iter().map(|p| self.node_ref(*p)).collect(),
                weakest_from: self.node_ref(c.weakest.0),
                weakest_to: self.node_ref(c.weakest.1),
                weakest_weight: c.weakest.2,
            })
            .collect()
    }

    fn violations(&self) -> Vec<ViolationDto> {
        self.atlas
            .violations
            .iter()
            .map(|v| ViolationDto {
                kind: match v.kind {
                    ViolationKind::NotExported => ViolationKindDto::NotExported,
                    ViolationKind::NotRead => ViolationKindDto::NotRead,
                },
                from_module: self.node_ref(v.from_module),
                to_package: self.node_ref(v.to_package),
                evidence: v
                    .evidence
                    .iter()
                    .map(|e| EvidenceDto {
                        file: e.file.clone(),
                        line: e.line,
                    })
                    .collect(),
            })
            .collect()
    }

    fn findings(&self) -> Vec<FindingDto> {
        self.atlas
            .findings
            .iter()
            .map(|f| {
                let (kind, reachable) = match f.kind {
                    FindingKind::UnusedModuleDependency => (FindingKindDto::UnusedModule, false),
                    FindingKind::UnusedLibrary => (FindingKindDto::UnusedLibrary, false),
                    FindingKind::UndeclaredModuleDependency { reachable } => {
                        (FindingKindDto::UndeclaredModule, reachable)
                    }
                    FindingKind::UndeclaredLibrary => (FindingKindDto::UndeclaredLibrary, false),
                };
                FindingDto {
                    kind,
                    reachable,
                    module: self.node_ref(f.module),
                    target: self.node_ref(f.target),
                }
            })
            .collect()
    }

    // -- views ----------------------------------------------------------------

    /// Does the road `from → to` carry a JPMS violation?
    fn crosses_border(&self, from: NodeId, to: NodeId) -> bool {
        self.atlas.violations.iter().any(|v| {
            // `from` lies in the offending module; `to` is the forbidden package,
            // a type inside it, or the region that contains it.
            self.atlas.contains(v.from_module, from)
                && (self.atlas.contains(to, v.to_package) || self.atlas.contains(v.to_package, to))
        })
    }

    pub fn view(&self, request: &ViewRequest) -> Result<AtlasViewDto, AppError> {
        let focus = request
            .focus
            .as_deref()
            .map(|k| self.node_id(k))
            .transpose()?;
        let filters = ViewFilters {
            tests: request.filters.tests,
            jdk: request.filters.jdk,
            external: request.filters.external,
        };
        let view = self.atlas.view(scale(request.scale), focus, filters);
        let edges = view
            .edges
            .iter()
            .map(|e| ViewEdgeDto {
                from: self.atlas.key(e.from),
                to: self.atlas.key(e.to),
                main: e.weight.main,
                test: e.weight.test,
                in_cycle: matches!(
                    (self.cycle_of.get(&e.from), self.cycle_of.get(&e.to)),
                    (Some(a), Some(b)) if a == b
                ),
                violation: self.crosses_border(e.from, e.to),
            })
            .collect();
        Ok(AtlasViewDto {
            scale: request.scale,
            focus: focus.map(|f| self.node_dto(f)),
            members: view.members.iter().map(|id| self.node_dto(*id)).collect(),
            neighbours: view
                .neighbours
                .iter()
                .map(|id| self.node_dto(*id))
                .collect(),
            edges,
        })
    }

    // -- details --------------------------------------------------------------

    fn links(&self, edges: &[Edge], pick: impl Fn(&Edge) -> Option<NodeId>) -> Vec<LinkDto> {
        let mut links: Vec<LinkDto> = edges
            .iter()
            .filter_map(|e| {
                let other = pick(e)?;
                Some(LinkDto {
                    node: self.node_ref(other),
                    kind: self.node_dto(other).kind,
                    main: e.weight.main,
                    test: e.weight.test,
                })
            })
            .collect();
        links.sort_by(|a, b| {
            (b.main + b.test)
                .cmp(&(a.main + a.test))
                .then_with(|| a.node.name.cmp(&b.node.name))
        });
        links
    }

    pub fn details(&self, key: &str) -> Result<NodeDetailsDto, AppError> {
        let id = self.node_id(key)?;
        let node = self.atlas.node(id);
        let level = match node.kind {
            NodeKind::Module { .. } => Level::Module,
            NodeKind::Package { .. } => Level::Package,
            NodeKind::Type { .. } => Level::Type,
            // External nodes are reached from every level; modules are the clearest.
            _ => Level::Module,
        };
        let edges = self.atlas.edges(level);
        let outgoing = self.links(edges, |e| (e.from == id).then_some(e.to));
        let incoming = self.links(edges, |e| (e.to == id).then_some(e.from));
        let children = self
            .atlas
            .nodes
            .iter()
            .filter(|n| n.parent == Some(id))
            .map(|n| self.node_ref(n.id))
            .collect();
        let (file, packages) = match &node.kind {
            NodeKind::Type { file, .. } => (Some(file.clone()), Vec::new()),
            NodeKind::Library { packages, .. } => (
                None,
                packages
                    .iter()
                    .map(|(p, m)| LibraryPackageDto {
                        package: p.clone(),
                        matched_by: method_name(*m).to_owned(),
                    })
                    .collect(),
            ),
            _ => (None, Vec::new()),
        };
        Ok(NodeDetailsDto {
            node: self.node_dto(id),
            outgoing,
            incoming,
            children,
            file,
            packages,
        })
    }

    pub fn evidence(&self, from: &str, to: &str) -> Result<Vec<EvidenceDto>, AppError> {
        let (from, to) = (self.node_id(from)?, self.node_id(to)?);
        Ok(self
            .atlas
            .evidence_between(from, to, 200)
            .into_iter()
            .map(|e| EvidenceDto {
                file: e.file,
                line: e.line,
            })
            .collect())
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::path::PathBuf;

    fn bank() -> OpenProject {
        let root = PathBuf::from(env!("CARGO_MANIFEST_DIR")).join("../samples/atlas-bank");
        scan(&root, &|_| {}).unwrap()
    }

    #[test]
    fn summary_counts_and_report() {
        let p = bank();
        let s = &p.summary;
        assert_eq!((s.modules, s.packages, s.files, s.libraries), (5, 8, 17, 4));
        assert_eq!(s.cycles.len(), 2);
        assert_eq!(s.violations.len(), 1);
        assert_eq!(
            s.violations[0].to_package.name,
            "com.atlasbank.persistence.internal"
        );
        assert_eq!(s.findings.len(), 3);
    }

    #[test]
    fn progress_arrives_in_stage_order() {
        let root = PathBuf::from(env!("CARGO_MANIFEST_DIR")).join("../samples/atlas-bank");
        let stages = Mutex::new(Vec::new());
        scan(&root, &|e| {
            let tag = match e {
                ScanEvent::ReadingBuild => "build".to_owned(),
                ScanEvent::ParsingSources { done, total } => format!("{done}/{total}"),
                ScanEvent::IndexingLibraries => "jars".to_owned(),
                ScanEvent::Analysing => "analysis".to_owned(),
            };
            stages.lock().unwrap().push(tag);
        })
        .unwrap();
        let stages = stages.into_inner().unwrap();
        assert_eq!(stages.first().map(String::as_str), Some("build"));
        // 17 source files + 3 module-info.java files are parsed.
        assert!(
            stages.contains(&"20/20".to_owned()),
            "the last file is always reported: {stages:?}"
        );
        assert_eq!(&stages[stages.len() - 2..], ["jars", "analysis"]);
    }

    #[test]
    fn country_view_marks_the_smuggling_road() {
        let p = bank();
        let request = ViewRequest {
            scale: ScaleDto::Country,
            focus: None,
            filters: ViewFiltersDto {
                tests: false,
                jdk: false,
                external: true,
            },
        };
        let view = p.view(&request).unwrap();
        let smuggling: Vec<(&str, &str)> = view
            .edges
            .iter()
            .filter(|e| e.violation)
            .map(|e| (e.from.as_str(), e.to.as_str()))
            .collect();
        assert_eq!(
            smuggling,
            [(
                "m:com.atlasbank:bank-api",
                "m:com.atlasbank:bank-persistence"
            )]
        );
        let service = view
            .members
            .iter()
            .find(|n| n.name == "bank-service")
            .unwrap();
        assert!(
            service.in_cycle,
            "the account/audit cycle lives in bank-service"
        );
    }

    #[test]
    fn region_view_marks_cycle_roads() {
        let p = bank();
        let request = ViewRequest {
            scale: ScaleDto::Region,
            focus: Some("m:com.atlasbank:bank-service".into()),
            filters: ViewFiltersDto {
                tests: false,
                jdk: false,
                external: true,
            },
        };
        let view = p.view(&request).unwrap();
        let cycle: Vec<(&str, &str)> = view
            .edges
            .iter()
            .filter(|e| e.in_cycle)
            .map(|e| (e.from.as_str(), e.to.as_str()))
            .collect();
        assert_eq!(cycle.len(), 2, "{cycle:?}");
    }

    #[test]
    fn details_and_evidence() {
        let p = bank();
        let d = p
            .details("p:com.atlasbank:bank-common|com.atlasbank.common")
            .unwrap();
        assert_eq!(d.children.len(), 2);
        assert_eq!(d.incoming.len(), 3);
        // common depends only on the JDK (java.math, java.util…).
        assert!(
            d.outgoing.iter().all(|l| l.kind == NodeKindDto::Jdk),
            "{:?}",
            d.outgoing
        );
        let ev = p
            .evidence(
                "m:com.atlasbank:bank-api",
                "p:com.atlasbank:bank-persistence|com.atlasbank.persistence.internal",
            )
            .unwrap();
        assert_eq!(ev.len(), 2);
        assert!(p.details("p:nope").is_err());
    }
}
