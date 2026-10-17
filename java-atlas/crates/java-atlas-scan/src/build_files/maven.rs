//! Maven: `pom.xml` → [`ProjectFacts`].
//!
//! Two steps, kept apart on purpose:
//!
//! 1. `parse_pom` reads *one* file into a `RawPom` — exactly what is
//!    written there, nothing inherited, nothing interpolated. Pure function,
//!    easy to test with a string.
//! 2. `MavenReader` walks the module tree, follows `<parent>` links inside
//!    the project and builds an `Effective` model per module: properties,
//!    `dependencyManagement` and `<dependencies>` merged down the chain.
//!
//! Not supported (documented limits): profiles, BOM imports
//! (`<scope>import</scope>`), parents outside the project, settings.xml.

use std::collections::{HashMap, HashSet};
use std::fs;
use std::path::{Path, PathBuf};
use std::rc::Rc;

use java_atlas_core::facts::{
    BuildSystem, ExternalDep, ModuleDep, ModuleFacts, ProjectFacts, ScanWarning, Scope, SourceRoot,
    VersionSource,
};
use quick_xml::Reader;
use quick_xml::events::Event;

use crate::ScanError;
use crate::paths::{normalize, relative_slash};

// ---------------------------------------------------------------------------
// Step 1: one file, as written
// ---------------------------------------------------------------------------

/// A `<dependency>` element, fields exactly as written (may contain `${...}`).
#[derive(Debug, Clone, Default, PartialEq, Eq)]
pub(crate) struct RawDep {
    pub group: Option<String>,
    pub artifact: Option<String>,
    pub version: Option<String>,
    pub scope: Option<String>,
}

/// The `<parent>` element.
#[derive(Debug, Clone, Default, PartialEq, Eq)]
pub(crate) struct ParentRef {
    pub group: Option<String>,
    pub artifact: Option<String>,
    pub version: Option<String>,
    /// `None`: not written (Maven's default `../pom.xml`).
    /// `Some("")`: `<relativePath/>`, i.e. "do not look on disk".
    pub relative_path: Option<String>,
}

/// One `pom.xml`, uninterpreted.
#[derive(Debug, Clone, Default, PartialEq, Eq)]
pub(crate) struct RawPom {
    pub group: Option<String>,
    pub artifact: Option<String>,
    pub version: Option<String>,
    pub packaging: Option<String>,
    pub parent: Option<ParentRef>,
    /// Declaration order is kept; later entries win on duplicates.
    pub properties: Vec<(String, String)>,
    pub modules: Vec<String>,
    pub dependencies: Vec<RawDep>,
    pub managed: Vec<RawDep>,
    /// `<build><sourceDirectory>` / `<testSourceDirectory>`; inherited by children.
    pub source_directory: Option<String>,
    pub test_source_directory: Option<String>,
}

const DEPENDENCY: &[&str] = &["project", "dependencies", "dependency"];
const MANAGED_DEPENDENCY: &[&str] = &[
    "project",
    "dependencyManagement",
    "dependencies",
    "dependency",
];

/// Parses one POM. Only the elements java-atlas needs are looked at; anything
/// else (build plugins, profiles, reporting…) is skipped by simply not matching
/// its path.
pub(crate) fn parse_pom(xml: &str) -> Result<RawPom, String> {
    let mut reader = Reader::from_str(xml);
    // Element names are matched by *local* name, so `<pom:project>` works too.
    let mut stack: Vec<String> = Vec::new();
    let mut text = String::new();
    let mut pom = RawPom::default();
    let mut dep: Option<RawDep> = None;

    loop {
        let event = reader
            .read_event()
            .map_err(|e| format!("XML error at byte {}: {e}", reader.error_position()))?;
        match event {
            Event::Start(e) => {
                stack.push(e.local_name().as_ref().to_owned());
                text.clear();
                open(&stack, &mut pom, &mut dep);
            }
            Event::Empty(e) => {
                // `<relativePath/>` = open + close with no text.
                stack.push(e.local_name().as_ref().to_owned());
                text.clear();
                open(&stack, &mut pom, &mut dep);
                close(&stack, "", &mut pom, &mut dep);
                stack.pop();
            }
            Event::Text(t) => text.push_str(&t.xml10_content()),
            Event::CData(c) => text.push_str(&c.xml10_content()),
            Event::GeneralRef(r) => {
                // `&amp;`, `&lt;`, `&#64;` … arrive as separate events.
                let ch = match r.resolve_char_ref().map_err(|e| e.to_string())? {
                    Some(ch) => Some(ch),
                    None => match r.as_ref() {
                        "amp" => Some('&'),
                        "lt" => Some('<'),
                        "gt" => Some('>'),
                        "quot" => Some('"'),
                        "apos" => Some('\''),
                        _ => None,
                    },
                };
                if let Some(ch) = ch {
                    text.push(ch);
                }
            }
            Event::End(_) => {
                close(&stack, text.trim(), &mut pom, &mut dep);
                stack.pop();
                text.clear();
            }
            Event::Eof => break,
            _ => {}
        }
    }
    Ok(pom)
}

/// Called when an element starts; `stack` already contains it.
fn open(stack: &[String], pom: &mut RawPom, dep: &mut Option<RawDep>) {
    let path: Vec<&str> = stack.iter().map(String::as_str).collect();
    if path == DEPENDENCY || path == MANAGED_DEPENDENCY {
        *dep = Some(RawDep::default());
    } else if path == ["project", "parent"] {
        pom.parent = Some(ParentRef::default());
    }
}

/// Called when an element ends; `stack` still contains it.
fn close(stack: &[String], text: &str, pom: &mut RawPom, dep: &mut Option<RawDep>) {
    let path: Vec<&str> = stack.iter().map(String::as_str).collect();
    let value = || (!text.is_empty()).then(|| text.to_owned());

    match path.as_slice() {
        ["project", "groupId"] => pom.group = value(),
        ["project", "artifactId"] => pom.artifact = value(),
        ["project", "version"] => pom.version = value(),
        ["project", "packaging"] => pom.packaging = value(),
        ["project", "build", "sourceDirectory"] => pom.source_directory = value(),
        ["project", "build", "testSourceDirectory"] => pom.test_source_directory = value(),

        ["project", "parent", field] => {
            if let Some(parent) = pom.parent.as_mut() {
                match *field {
                    "groupId" => parent.group = value(),
                    "artifactId" => parent.artifact = value(),
                    "version" => parent.version = value(),
                    // An empty value is meaningful here, so no `value()`.
                    "relativePath" => parent.relative_path = Some(text.to_owned()),
                    _ => {}
                }
            }
        }

        ["project", "properties", name] => {
            pom.properties.push(((*name).to_owned(), text.to_owned()))
        }
        ["project", "modules", "module"] => {
            if let Some(m) = value() {
                pom.modules.push(m);
            }
        }

        p if p == DEPENDENCY => {
            if let Some(d) = dep.take() {
                pom.dependencies.push(d);
            }
        }
        p if p == MANAGED_DEPENDENCY => {
            if let Some(d) = dep.take() {
                pom.managed.push(d);
            }
        }
        // A field of a <dependency> under one of the two dependency lists.
        [parent @ .., field] if parent == DEPENDENCY || parent == MANAGED_DEPENDENCY => {
            if let Some(d) = dep.as_mut() {
                match *field {
                    "groupId" => d.group = value(),
                    "artifactId" => d.artifact = value(),
                    "version" => d.version = value(),
                    "scope" => d.scope = value(),
                    _ => {}
                }
            }
        }
        _ => {}
    }
}

// ---------------------------------------------------------------------------
// Property interpolation
// ---------------------------------------------------------------------------

/// Result of expanding `${...}` placeholders in one string.
#[derive(Debug, PartialEq, Eq)]
pub(crate) struct Interpolated {
    pub value: String,
    /// The first property used, reported as the version's source.
    pub first_property: Option<String>,
    /// Properties that had no value; left as `${name}` in `value`.
    pub unresolved: Vec<String>,
}

const MAX_DEPTH: usize = 8;

pub(crate) fn interpolate(raw: &str, props: &HashMap<String, String>) -> Interpolated {
    let mut first = None;
    let mut unresolved = Vec::new();
    let value = expand(raw, props, 0, &mut first, &mut unresolved);
    Interpolated {
        value,
        first_property: first,
        unresolved,
    }
}

fn expand(
    raw: &str,
    props: &HashMap<String, String>,
    depth: usize,
    first: &mut Option<String>,
    unresolved: &mut Vec<String>,
) -> String {
    let mut out = String::with_capacity(raw.len());
    let mut rest = raw;
    while let Some(start) = rest.find("${") {
        out.push_str(&rest[..start]);
        let after = &rest[start + 2..];
        let Some(end) = after.find('}') else {
            // `${` without a closing brace: keep the text as it is.
            out.push_str(&rest[start..]);
            return out;
        };
        let name = &after[..end];
        if first.is_none() {
            *first = Some(name.to_owned());
        }
        match props.get(name) {
            // A property may refer to other properties; guard against cycles.
            Some(v) if depth < MAX_DEPTH => {
                out.push_str(&expand(v, props, depth + 1, first, unresolved))
            }
            _ => {
                unresolved.push(name.to_owned());
                out.push_str(&rest[start..start + 2 + end + 1]);
            }
        }
        rest = &after[end + 1..];
    }
    out.push_str(rest);
    out
}

// ---------------------------------------------------------------------------
// Step 2: the effective model of a module
// ---------------------------------------------------------------------------

/// A module after inheritance. Values may still hold `${...}`; they are
/// interpolated with [`Effective::props`] when used.
#[derive(Debug)]
struct Effective {
    raw: RawPom,
    group: String,
    artifact: String,
    version: String,
    props: HashMap<String, String>,
    /// `group:artifact` → managing declaration (raw, interpolate on use).
    managed: HashMap<String, RawDep>,
    /// Declared dependencies; `true` = inherited from a parent POM.
    deps: Vec<(RawDep, bool)>,
    /// Coordinates of a parent that is not part of the project.
    outside_parent: Option<String>,
    source_directory: Option<String>,
    test_source_directory: Option<String>,
}

struct MavenReader {
    root: PathBuf,
    cache: HashMap<PathBuf, Rc<Effective>>,
    in_progress: HashSet<PathBuf>,
    warnings: Vec<ScanWarning>,
}

impl MavenReader {
    fn warn(&mut self, file: &Path, message: impl Into<String>) {
        let file = relative_slash(&self.root, file);
        self.warnings.push(ScanWarning {
            file,
            message: message.into(),
        });
    }

    fn read_raw(&self, pom_path: &Path) -> Result<RawPom, ScanError> {
        let xml = fs::read_to_string(pom_path).map_err(|source| ScanError::Io {
            path: pom_path.display().to_string(),
            source,
        })?;
        parse_pom(&xml).map_err(|message| ScanError::Xml {
            path: pom_path.display().to_string(),
            message,
        })
    }

    /// Builds (or returns the cached) effective model of the POM at `pom_path`.
    fn effective(&mut self, pom_path: &Path) -> Result<Rc<Effective>, ScanError> {
        let pom_path = normalize(pom_path);
        if let Some(done) = self.cache.get(&pom_path) {
            return Ok(Rc::clone(done));
        }
        if !self.in_progress.insert(pom_path.clone()) {
            // A parent chain that loops back on itself; Maven rejects it too.
            return Err(ScanError::Xml {
                path: pom_path.display().to_string(),
                message: "parent chain forms a cycle".into(),
            });
        }

        let raw = self.read_raw(&pom_path)?;
        let parent = self.load_parent(&pom_path, &raw)?;
        let effective = Rc::new(merge(raw, parent.as_deref(), &pom_path, self));

        self.in_progress.remove(&pom_path);
        self.cache.insert(pom_path, Rc::clone(&effective));
        Ok(effective)
    }

    /// Follows `<parent>` if it points to a POM inside the project.
    fn load_parent(
        &mut self,
        pom_path: &Path,
        raw: &RawPom,
    ) -> Result<Option<Rc<Effective>>, ScanError> {
        let Some(parent) = &raw.parent else {
            return Ok(None);
        };
        let dir = pom_path.parent().unwrap_or(Path::new(""));

        let candidate = match parent.relative_path.as_deref() {
            Some("") => None,
            Some(rel) if rel.ends_with(".xml") => Some(dir.join(rel)),
            Some(rel) => Some(dir.join(rel).join("pom.xml")),
            None => Some(dir.join("..").join("pom.xml")),
        }
        .map(|p| normalize(&p))
        .filter(|p| p.starts_with(&self.root) && p.is_file());

        if let Some(candidate) = candidate {
            let loaded = self.effective(&candidate)?;
            // The file on disk must really be the declared parent.
            if Some(&loaded.artifact) == parent.artifact.as_ref() {
                return Ok(Some(loaded));
            }
        }
        Ok(None)
    }
}

/// Merges a POM with its (already effective) parent.
fn merge(
    raw: RawPom,
    parent: Option<&Effective>,
    pom_path: &Path,
    reader: &mut MavenReader,
) -> Effective {
    let parent_ref = raw.parent.clone().unwrap_or_default();

    let group = raw
        .group
        .clone()
        .or_else(|| parent.map(|p| p.group.clone()))
        .or_else(|| parent_ref.group.clone())
        .unwrap_or_default();
    let artifact = raw.artifact.clone().unwrap_or_default();
    let version = raw
        .version
        .clone()
        .or_else(|| parent.map(|p| p.version.clone()))
        .or_else(|| parent_ref.version.clone())
        .unwrap_or_default();

    // Properties: parent first, own values override, built-ins last.
    let mut props = parent.map(|p| p.props.clone()).unwrap_or_default();
    props.extend(raw.properties.iter().cloned());
    for key in ["project.groupId", "pom.groupId"] {
        props.insert(key.into(), group.clone());
    }
    for key in ["project.artifactId", "pom.artifactId"] {
        props.insert(key.into(), artifact.clone());
    }
    for key in ["project.version", "pom.version", "version"] {
        props.insert(key.into(), version.clone());
    }
    if let Some(v) = &parent_ref.version {
        props.insert("project.parent.version".into(), v.clone());
    }
    if let Some(g) = &parent_ref.group {
        props.insert("project.parent.groupId".into(), g.clone());
    }

    let key_of = |d: &RawDep, props: &HashMap<String, String>| {
        let g = interpolate(d.group.as_deref().unwrap_or_default(), props).value;
        let a = interpolate(d.artifact.as_deref().unwrap_or_default(), props).value;
        format!("{g}:{a}")
    };

    let mut managed = parent.map(|p| p.managed.clone()).unwrap_or_default();
    for d in &raw.managed {
        if d.scope.as_deref() == Some("import") {
            reader.warn(
                pom_path,
                format!("BOM import {} is not followed", key_of(d, &props)),
            );
            continue;
        }
        managed.insert(key_of(d, &props), d.clone());
    }

    // Inherited dependencies first; a module may redeclare one to change it.
    let mut deps: Vec<(RawDep, bool)> = parent
        .map(|p| p.deps.iter().map(|(d, _)| (d.clone(), true)).collect())
        .unwrap_or_default();
    for d in &raw.dependencies {
        let key = key_of(d, &props);
        deps.retain(|(existing, _)| key_of(existing, &props) != key);
        deps.push((d.clone(), false));
    }

    let outside_parent = match (&raw.parent, parent) {
        (Some(p), None) => {
            let coords = format!(
                "{}:{}:{}",
                p.group.as_deref().unwrap_or("?"),
                p.artifact.as_deref().unwrap_or("?"),
                p.version.as_deref().unwrap_or("?")
            );
            reader.warn(
                pom_path,
                format!("parent {coords} is outside the project; its managed versions are unknown"),
            );
            Some(coords)
        }
        (None, _) => None,
        (Some(_), Some(p)) => p.outside_parent.clone(),
    };

    // Build folders are inherited, but resolved against each module's own folder.
    let source_directory = raw
        .source_directory
        .clone()
        .or_else(|| parent.and_then(|p| p.source_directory.clone()));
    let test_source_directory = raw
        .test_source_directory
        .clone()
        .or_else(|| parent.and_then(|p| p.test_source_directory.clone()));

    Effective {
        raw,
        group,
        artifact,
        version,
        props,
        managed,
        deps,
        outside_parent,
        source_directory,
        test_source_directory,
    }
}

/// Declared source folders, relative to the module folder. Empty when the
/// module follows the convention.
fn source_roots(m: &Effective) -> Vec<SourceRoot> {
    if m.source_directory.is_none() && m.test_source_directory.is_none() {
        return Vec::new();
    }
    // `${project.basedir}/src` and `src` mean the same folder.
    let mut props = m.props.clone();
    for key in ["project.basedir", "basedir"] {
        props.insert(key.into(), String::new());
    }
    let resolve = |dir: &Option<String>, default: &str| {
        dir.as_deref()
            .map(|d| {
                interpolate(d, &props)
                    .value
                    .trim_start_matches('/')
                    .replace('\\', "/")
            })
            .unwrap_or_else(|| default.to_owned())
    };
    vec![
        SourceRoot {
            path: resolve(&m.source_directory, "src/main/java"),
            is_test: false,
        },
        SourceRoot {
            path: resolve(&m.test_source_directory, "src/test/java"),
            is_test: true,
        },
    ]
}

fn scope_of(word: &str) -> Option<Scope> {
    match word {
        "compile" => Some(Scope::Compile),
        "provided" | "system" => Some(Scope::Provided),
        "runtime" => Some(Scope::Runtime),
        "test" => Some(Scope::Test),
        _ => None, // "import" and typos
    }
}

/// Collects module POMs depth-first, root first, in `<modules>` order.
fn collect_modules(
    reader: &mut MavenReader,
    pom: &Path,
    out: &mut Vec<PathBuf>,
) -> Result<(), ScanError> {
    let pom = normalize(pom);
    if out.contains(&pom) {
        return Ok(());
    }
    let effective = reader.effective(&pom)?;
    out.push(pom.clone());

    let dir = pom.parent().unwrap_or(Path::new("")).to_path_buf();
    for module in &effective.raw.modules {
        let child = if module.ends_with(".xml") {
            dir.join(module)
        } else {
            dir.join(module).join("pom.xml")
        };
        if child.is_file() {
            collect_modules(reader, &child, out)?;
        } else {
            reader.warn(&pom, format!("module '{module}' has no pom.xml"));
        }
    }
    Ok(())
}

/// Reads a Maven project rooted at `root` (which must contain `pom.xml`).
pub fn read(root: &Path) -> Result<ProjectFacts, ScanError> {
    let root = normalize(root);
    let mut reader = MavenReader {
        root: root.clone(),
        cache: HashMap::new(),
        in_progress: HashSet::new(),
        warnings: Vec::new(),
    };

    let mut poms = Vec::new();
    collect_modules(&mut reader, &root.join("pom.xml"), &mut poms)?;
    let models: Vec<(PathBuf, Rc<Effective>)> = poms
        .into_iter()
        .map(|p| {
            let e = reader.effective(&p)?;
            Ok((p, e))
        })
        .collect::<Result<_, ScanError>>()?;

    // Which `group:artifact` pairs are modules of this project?
    let internal: HashSet<String> = models
        .iter()
        .map(|(_, m)| format!("{}:{}", m.group, m.artifact))
        .collect();

    let mut modules = Vec::with_capacity(models.len());
    for (pom_path, m) in &models {
        let id = format!("{}:{}", m.group, m.artifact);
        let dir = pom_path.parent().unwrap_or(Path::new(""));
        let mut module = ModuleFacts {
            id: id.clone(),
            name: m.artifact.clone(),
            path: relative_slash(&root, dir),
            build_file: relative_slash(&root, pom_path),
            aggregator: m.raw.packaging.as_deref() == Some("pom"),
            source_roots: source_roots(m),
            jpms: None,
            files: Vec::new(),
            module_deps: Vec::new(),
            external_deps: Vec::new(),
        };

        for (d, inherited) in &m.deps {
            let group = interpolate(d.group.as_deref().unwrap_or_default(), &m.props).value;
            let artifact = interpolate(d.artifact.as_deref().unwrap_or_default(), &m.props).value;
            let key = format!("{group}:{artifact}");
            let managed = m.managed.get(&key);

            let scope_word = d
                .scope
                .as_deref()
                .or_else(|| managed.and_then(|md| md.scope.as_deref()))
                .unwrap_or("compile");
            let Some(scope) = scope_of(scope_word) else {
                reader.warn(
                    pom_path,
                    format!("{key}: unknown scope '{scope_word}' skipped"),
                );
                continue;
            };

            if internal.contains(&key) {
                if key != id {
                    module.module_deps.push(ModuleDep {
                        target: key,
                        scope,
                        exposed: false,
                    });
                }
                continue;
            }

            let (version, version_source) =
                match (&d.version, managed.and_then(|md| md.version.as_ref())) {
                    (Some(v), _) => {
                        let i = interpolate(v, &m.props);
                        match (i.unresolved.is_empty(), i.first_property) {
                            (true, Some(p)) => (Some(i.value), VersionSource::Property(p)),
                            (true, None) => (Some(i.value), VersionSource::Explicit),
                            (false, _) => {
                                reader.warn(
                                    pom_path,
                                    format!("{key}: unresolved ${{{}}}", i.unresolved[0]),
                                );
                                (None, VersionSource::Unresolved)
                            }
                        }
                    }
                    (None, Some(v)) => {
                        let i = interpolate(v, &m.props);
                        if i.unresolved.is_empty() {
                            (Some(i.value), VersionSource::Managed)
                        } else {
                            reader.warn(
                                pom_path,
                                format!("{key}: unresolved ${{{}}}", i.unresolved[0]),
                            );
                            (None, VersionSource::Unresolved)
                        }
                    }
                    (None, None) => {
                        if m.outside_parent.is_none() {
                            reader.warn(
                                pom_path,
                                format!("{key}: no version and no dependencyManagement entry"),
                            );
                        }
                        (None, VersionSource::Unresolved)
                    }
                };

            module.external_deps.push(ExternalDep {
                group,
                artifact,
                version,
                version_source,
                scope,
                inherited: *inherited,
            });
        }
        modules.push(module);
    }

    let name = models
        .first()
        .map(|(_, m)| m.artifact.clone())
        .unwrap_or_default();
    Ok(ProjectFacts {
        name,
        build_system: BuildSystem::Maven,
        modules,
        warnings: reader.warnings,
        libraries: Vec::new(),
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    const POM: &str = r#"<?xml version="1.0"?>
<project xmlns="http://maven.apache.org/POM/4.0.0">
  <parent>
    <groupId>g</groupId><artifactId>root</artifactId><version>1</version>
    <relativePath/>
  </parent>
  <artifactId>app</artifactId>
  <properties><lib.version>2.0</lib.version></properties>
  <modules><module>child</module></modules>
  <dependencies>
    <dependency>
      <groupId>org.x</groupId><artifactId>lib</artifactId>
      <version>${lib.version}</version><scope>test</scope>
    </dependency>
  </dependencies>
  <dependencyManagement><dependencies>
    <dependency><groupId>org.y</groupId><artifactId>managed</artifactId><version>3</version></dependency>
  </dependencies></dependencyManagement>
  <build><plugins><plugin>
    <dependencies><dependency><groupId>no</groupId><artifactId>plugin-dep</artifactId></dependency></dependencies>
  </plugin></plugins></build>
</project>"#;

    #[test]
    fn reads_what_is_written_and_nothing_more() {
        let pom = parse_pom(POM).unwrap();
        assert_eq!(pom.artifact.as_deref(), Some("app"));
        assert_eq!(pom.group, None, "groupId is inherited, not written");
        assert_eq!(pom.modules, vec!["child"]);
        assert_eq!(
            pom.properties,
            vec![("lib.version".to_owned(), "2.0".to_owned())]
        );
        assert_eq!(pom.dependencies.len(), 1, "plugin dependencies are ignored");
        assert_eq!(
            pom.dependencies[0].version.as_deref(),
            Some("${lib.version}")
        );
        assert_eq!(pom.managed.len(), 1);
    }

    #[test]
    fn an_empty_relative_path_is_kept_as_empty() {
        let pom = parse_pom(POM).unwrap();
        assert_eq!(pom.parent.unwrap().relative_path.as_deref(), Some(""));
    }

    #[test]
    fn entities_are_decoded() {
        let pom =
            parse_pom("<project><name>A &amp; B</name><artifactId>a&#45;b</artifactId></project>")
                .unwrap();
        assert_eq!(pom.artifact.as_deref(), Some("a-b"));
    }

    #[test]
    fn broken_xml_is_an_error_with_a_position() {
        let err = parse_pom("<project><artifactId>x</groupId></project>").unwrap_err();
        assert!(err.contains("byte"), "{err}");
    }

    fn props(pairs: &[(&str, &str)]) -> HashMap<String, String> {
        pairs
            .iter()
            .map(|(k, v)| ((*k).to_owned(), (*v).to_owned()))
            .collect()
    }

    #[test]
    fn properties_expand_recursively() {
        let p = props(&[("a", "${b}-x"), ("b", "1.0")]);
        let i = interpolate("v${a}", &p);
        assert_eq!(i.value, "v1.0-x");
        assert_eq!(i.first_property.as_deref(), Some("a"));
        assert!(i.unresolved.is_empty());
    }

    #[test]
    fn unknown_properties_are_reported_and_kept() {
        let i = interpolate("${nope}", &HashMap::new());
        assert_eq!(i.value, "${nope}");
        assert_eq!(i.unresolved, vec!["nope"]);
    }

    #[test]
    fn self_referencing_properties_do_not_loop_forever() {
        let p = props(&[("a", "${a}")]);
        let i = interpolate("${a}", &p);
        assert_eq!(i.unresolved, vec!["a"]);
    }
}
