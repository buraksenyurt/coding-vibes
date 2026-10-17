//! Gradle version catalogs: `gradle/libs.versions.toml`.
//!
//! The catalog maps *aliases* to coordinates. Build scripts refer to them
//! through generated accessors where `-`, `_` and `.` all become `.`:
//! the alias `junit-jupiter` is written `libs.junit.jupiter`.

use std::collections::HashMap;

use toml::{Table, Value};

#[derive(Debug, Clone, PartialEq, Eq)]
pub(crate) struct Library {
    pub group: String,
    pub artifact: String,
    pub version: Option<String>,
}

#[derive(Debug, Default)]
pub(crate) struct Catalog {
    /// Accessor path (`junit.jupiter`) → library.
    pub libraries: HashMap<String, Library>,
    /// Bundle accessor (`logging`) → library accessors.
    pub bundles: HashMap<String, Vec<String>>,
}

/// `junit-jupiter` / `junit_jupiter` / `junit.jupiter` → `junit.jupiter`.
pub(crate) fn accessor(alias: &str) -> String {
    alias.replace(['-', '_'], ".")
}

/// A version is either a string or a table: `{ ref = "x" }`, `{ strictly = "1.0" }`…
fn version_of(value: &Value, versions: &Table) -> Option<String> {
    match value {
        Value::String(v) => Some(v.clone()),
        Value::Table(t) => {
            if let Some(Value::String(r)) = t.get("ref") {
                return versions.get(r).and_then(|v| version_of(v, versions));
            }
            ["strictly", "require", "prefer"]
                .iter()
                .find_map(|k| t.get(*k).and_then(Value::as_str).map(str::to_owned))
        }
        _ => None,
    }
}

/// `"g:a"` or `"g:a:v"`.
fn split_coordinates(s: &str) -> Option<(String, String, Option<String>)> {
    let mut parts = s.split(':');
    let group = parts.next()?.to_owned();
    let artifact = parts.next()?.to_owned();
    let version = parts.next().map(str::to_owned);
    Some((group, artifact, version))
}

fn library_of(value: &Value, versions: &Table) -> Option<Library> {
    match value {
        // slf4j-simple = "org.slf4j:slf4j-simple:2.0.16"
        Value::String(s) => {
            let (group, artifact, version) = split_coordinates(s)?;
            Some(Library {
                group,
                artifact,
                version,
            })
        }
        // guava = { module = "g:a", version.ref = "guava" }
        // jackson = { group = "g", name = "a", version = "1" }
        Value::Table(t) => {
            let (group, artifact) = match t.get("module").and_then(Value::as_str) {
                Some(module) => {
                    let (g, a, _) = split_coordinates(module)?;
                    (g, a)
                }
                None => (
                    t.get("group")?.as_str()?.to_owned(),
                    t.get("name")?.as_str()?.to_owned(),
                ),
            };
            let version = t.get("version").and_then(|v| version_of(v, versions));
            Some(Library {
                group,
                artifact,
                version,
            })
        }
        _ => None,
    }
}

/// Parses the catalog. Entries that cannot be understood are skipped and
/// returned by alias, so the caller can warn about them.
pub(crate) fn parse_catalog(src: &str) -> Result<(Catalog, Vec<String>), String> {
    let doc: Table = src.parse().map_err(|e: toml::de::Error| e.to_string())?;
    let empty = Table::new();
    let versions = doc
        .get("versions")
        .and_then(Value::as_table)
        .unwrap_or(&empty);

    let mut catalog = Catalog::default();
    let mut skipped = Vec::new();

    if let Some(libraries) = doc.get("libraries").and_then(Value::as_table) {
        for (alias, value) in libraries {
            match library_of(value, versions) {
                Some(lib) => {
                    catalog.libraries.insert(accessor(alias), lib);
                }
                None => skipped.push(alias.clone()),
            }
        }
    }
    if let Some(bundles) = doc.get("bundles").and_then(Value::as_table) {
        for (name, members) in bundles {
            let members = members
                .as_array()
                .map(|a| a.iter().filter_map(Value::as_str).map(accessor).collect())
                .unwrap_or_default();
            catalog.bundles.insert(accessor(name), members);
        }
    }
    Ok((catalog, skipped))
}

#[cfg(test)]
mod tests {
    use super::*;

    const TOML: &str = r#"
[versions]
junit = "5.11.4"
pinned = { strictly = "1.2.3" }

[libraries]
junit-jupiter = { module = "org.junit.jupiter:junit-jupiter", version.ref = "junit" }
jackson_databind = { group = "com.fasterxml.jackson.core", name = "jackson-databind", version = "2.18.2" }
simple = "org.slf4j:slf4j-simple:2.0.16"
no-version = "org.x:y"
strict = { module = "a:b", version.ref = "pinned" }
broken = 42

[bundles]
test-stack = ["junit-jupiter", "simple"]
"#;

    #[test]
    fn every_notation_resolves() {
        let (c, skipped) = parse_catalog(TOML).unwrap();
        let v = |acc: &str| c.libraries[acc].version.clone();
        assert_eq!(v("junit.jupiter").as_deref(), Some("5.11.4"));
        assert_eq!(c.libraries["jackson.databind"].artifact, "jackson-databind");
        assert_eq!(v("simple").as_deref(), Some("2.0.16"));
        assert_eq!(v("no.version"), None);
        assert_eq!(v("strict").as_deref(), Some("1.2.3"));
        assert_eq!(skipped, vec!["broken"]);
    }

    #[test]
    fn bundles_list_accessors() {
        let (c, _) = parse_catalog(TOML).unwrap();
        assert_eq!(c.bundles["test.stack"], vec!["junit.jupiter", "simple"]);
    }

    #[test]
    fn invalid_toml_is_an_error() {
        assert!(parse_catalog("[libraries\nx = ").is_err());
    }
}
