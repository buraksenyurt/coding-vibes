//! Shapes that cross the IPC boundary.
//!
//! The core speaks in `NodeId` indices and rich enums; the front end gets
//! stable string keys, plain numbers and string tags. `ts-rs` writes a
//! matching TypeScript file for every `#[ts(export)]` type when `cargo test`
//! runs (into `src/lib/bindings`, see `.cargo/config.toml`), so the two sides
//! cannot drift apart silently.

use serde::{Deserialize, Serialize};
use ts_rs::TS;

// ---------------------------------------------------------------------------
// Scanning
// ---------------------------------------------------------------------------

/// Streamed through a `Channel` while a project is being scanned.
#[derive(Debug, Clone, Serialize, TS)]
#[serde(tag = "stage", rename_all = "camelCase")]
#[ts(export)]
pub enum ScanEvent {
    ReadingBuild,
    ParsingSources { done: u32, total: u32 },
    IndexingLibraries,
    Analysing,
}

#[derive(Debug, Clone, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct WarningDto {
    pub file: String,
    pub message: String,
}

/// Everything the toolbar and the report panel need after a scan.
#[derive(Debug, Clone, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct ProjectSummary {
    pub name: String,
    pub root: String,
    pub build_system: String,
    pub modules: u32,
    pub packages: u32,
    pub types: u32,
    pub files: u32,
    pub libraries: u32,
    pub unknown_packages: u32,
    /// Libraries whose jar was found in a local cache.
    pub indexed_jars: u32,
    pub scan_ms: u32,
    pub analysis_ms: u32,
    pub cycles: Vec<CycleDto>,
    pub violations: Vec<ViolationDto>,
    pub findings: Vec<FindingDto>,
    pub warnings: Vec<WarningDto>,
}

#[derive(Debug, Clone, PartialEq, Eq, Serialize, Deserialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct RecentProject {
    pub name: String,
    pub path: String,
    pub build_system: String,
}

// ---------------------------------------------------------------------------
// Report items
// ---------------------------------------------------------------------------

/// A node mentioned in a report line: key to navigate, name to display.
#[derive(Debug, Clone, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct NodeRef {
    pub key: String,
    pub name: String,
}

#[derive(Debug, Clone, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct EvidenceDto {
    pub file: String,
    pub line: u32,
}

#[derive(Debug, Clone, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct CycleDto {
    pub packages: Vec<NodeRef>,
    pub weakest_from: NodeRef,
    pub weakest_to: NodeRef,
    pub weakest_weight: u32,
}

#[derive(Debug, Clone, Copy, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub enum ViolationKindDto {
    NotExported,
    NotRead,
}

#[derive(Debug, Clone, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct ViolationDto {
    pub kind: ViolationKindDto,
    pub from_module: NodeRef,
    pub to_package: NodeRef,
    pub evidence: Vec<EvidenceDto>,
}

#[derive(Debug, Clone, Copy, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub enum FindingKindDto {
    UnusedModule,
    UnusedLibrary,
    UndeclaredModule,
    UndeclaredLibrary,
}

#[derive(Debug, Clone, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct FindingDto {
    pub kind: FindingKindDto,
    /// For undeclared module dependencies: still compiles thanks to a transitive path.
    pub reachable: bool,
    pub module: NodeRef,
    pub target: NodeRef,
}

// ---------------------------------------------------------------------------
// Views
// ---------------------------------------------------------------------------

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, Deserialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub enum ScaleDto {
    Country,
    Region,
    City,
}

#[derive(Debug, Clone, Copy, Serialize, Deserialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct ViewFiltersDto {
    pub tests: bool,
    pub jdk: bool,
    pub external: bool,
}

#[derive(Debug, Clone, Serialize, Deserialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct ViewRequest {
    pub scale: ScaleDto,
    /// Key of the module (region scale) or package (city scale).
    pub focus: Option<String>,
    pub filters: ViewFiltersDto,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub enum NodeKindDto {
    Module,
    Package,
    Type,
    Library,
    Jdk,
    Unknown,
}

#[derive(Debug, Clone, Copy, PartialEq, Eq, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub enum TypeKindDto {
    Class,
    Interface,
    Enum,
    Record,
    Annotation,
}

#[derive(Debug, Clone, Copy, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct MetricsDto {
    pub ca: u32,
    pub ce: u32,
    pub instability: Option<f64>,
}

/// A rectangle in world units (y grows downwards, like SVG).
#[derive(Debug, Clone, Copy, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct RectDto {
    pub x: f64,
    pub y: f64,
    pub w: f64,
    pub h: f64,
}

/// The faint shape of a child: city outlines inside a region, building
/// outlines inside a city. Texture, not interaction.
#[derive(Debug, Clone, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct OutlineDto {
    pub key: String,
    pub parent: String,
    pub rect: RectDto,
    pub is_test: bool,
    pub type_kind: Option<TypeKindDto>,
}

#[derive(Debug, Clone, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct ArchipelagoDto {
    /// groupId, or "?" for packages no library explains.
    pub name: String,
    pub frame: RectDto,
}

/// One drawable thing: region, city, building, island or mainland.
#[derive(Debug, Clone, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct NodeDto {
    pub key: String,
    pub kind: NodeKindDto,
    pub name: String,
    pub fqn: String,
    pub parent: Option<String>,
    pub is_test: bool,
    /// Lines of code under the node; sizes it on the map.
    pub size: u32,
    pub type_kind: Option<TypeKindDto>,
    /// Packages of JPMS modules: is there a gate in the border?
    pub exported: Option<bool>,
    /// Modules: the JPMS module name, if any.
    pub jpms: Option<String>,
    /// Packages (and their modules): part of a dependency cycle.
    pub in_cycle: bool,
    pub metrics: Option<MetricsDto>,
    /// Libraries: version from the build file.
    pub version: Option<String>,
    /// Libraries: "jar", "name", "mixed" or `None` when unused.
    pub matched_by: Option<String>,
    /// Where it sits on the map.
    pub rect: Option<RectDto>,
}

#[derive(Debug, Clone, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct ViewEdgeDto {
    pub from: String,
    pub to: String,
    pub main: u32,
    pub test: u32,
    /// Both ends belong to the same dependency cycle.
    pub in_cycle: bool,
    /// The road crosses a JPMS border without a gate.
    pub violation: bool,
}

#[derive(Debug, Clone, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct AtlasViewDto {
    pub scale: ScaleDto,
    pub focus: Option<NodeDto>,
    pub members: Vec<NodeDto>,
    pub neighbours: Vec<NodeDto>,
    pub edges: Vec<ViewEdgeDto>,
    /// The whole map, for "fit to window".
    pub world: RectDto,
    pub land: RectDto,
    /// The JDK coast, when the JDK filter is on and the code uses the JDK.
    pub mainland: Option<RectDto>,
    pub archipelagos: Vec<ArchipelagoDto>,
    pub outlines: Vec<OutlineDto>,
}

/// A neighbour in the details panel.
#[derive(Debug, Clone, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct LinkDto {
    pub node: NodeRef,
    pub kind: NodeKindDto,
    pub main: u32,
    pub test: u32,
}

#[derive(Debug, Clone, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct NodeDetailsDto {
    pub node: NodeDto,
    /// What this node depends on, at its own level, heaviest first.
    pub outgoing: Vec<LinkDto>,
    /// Who depends on it.
    pub incoming: Vec<LinkDto>,
    /// Packages of a module, types of a package.
    pub children: Vec<NodeRef>,
    /// Types: the source file.
    pub file: Option<String>,
    /// Libraries: the packages attributed to them and how.
    pub packages: Vec<LibraryPackageDto>,
}

#[derive(Debug, Clone, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct LibraryPackageDto {
    pub package: String,
    /// "jar" or "name".
    pub matched_by: String,
}
