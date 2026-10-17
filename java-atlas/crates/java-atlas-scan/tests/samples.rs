//! The two sample projects under `samples/`, read end to end.
//!
//! Snapshots live in `tests/snapshots/`. After an intentional change run
//! `cargo insta review` (or `INSTA_UPDATE=always cargo test`) and commit them.

use std::path::PathBuf;

use java_atlas_core::facts::{BuildSystem, Scope, VersionSource};
use java_atlas_scan::{ScanError, read_build};

fn sample(name: &str) -> PathBuf {
    PathBuf::from(env!("CARGO_MANIFEST_DIR"))
        .join("../../samples")
        .join(name)
}

#[test]
fn atlas_bank_snapshot() {
    let facts = read_build(&sample("atlas-bank")).unwrap();
    insta::assert_snapshot!(facts.describe());
}

#[test]
fn atlas_bank_module_graph() {
    let facts = read_build(&sample("atlas-bank")).unwrap();
    assert_eq!(facts.build_system, BuildSystem::Maven);
    assert_eq!(facts.modules.len(), 6, "root aggregator + 5 modules");
    assert!(facts.modules[0].aggregator);

    let targets = |id: &str| -> Vec<String> {
        facts
            .module(id)
            .unwrap()
            .module_deps
            .iter()
            .map(|d| d.target.clone())
            .collect()
    };
    assert_eq!(targets("com.atlasbank:bank-common"), Vec::<String>::new());
    assert_eq!(
        targets("com.atlasbank:bank-domain"),
        ["com.atlasbank:bank-common"]
    );
    assert_eq!(
        targets("com.atlasbank:bank-service"),
        [
            "com.atlasbank:bank-domain",
            "com.atlasbank:bank-persistence"
        ]
    );
    // bank-persistence is used by AccountController but not declared.
    assert_eq!(
        targets("com.atlasbank:bank-api"),
        ["com.atlasbank:bank-service"]
    );
    assert!(facts.warnings.is_empty(), "{:?}", facts.warnings);
}

#[test]
fn atlas_bank_versions_come_from_the_right_place() {
    let facts = read_build(&sample("atlas-bank")).unwrap();
    let api = facts.module("com.atlasbank:bank-api").unwrap();
    let dep = |artifact: &str| {
        api.external_deps
            .iter()
            .find(|d| d.artifact == artifact)
            .unwrap()
    };

    let jackson = dep("jackson-databind");
    assert_eq!(jackson.version.as_deref(), Some("2.18.2"));
    assert_eq!(
        jackson.version_source,
        VersionSource::Property("jackson.version".into())
    );

    let slf4j = dep("slf4j-api");
    assert_eq!(slf4j.version.as_deref(), Some("2.0.16"));
    assert_eq!(slf4j.version_source, VersionSource::Managed);

    let junit = dep("junit-jupiter");
    assert!(junit.inherited, "declared once in the parent POM");
    assert_eq!(junit.scope, Scope::Test);
}

#[test]
fn atlas_shop_snapshot() {
    let facts = read_build(&sample("atlas-shop-gradle")).unwrap();
    insta::assert_snapshot!(facts.describe());
}

#[test]
fn atlas_shop_module_graph() {
    let facts = read_build(&sample("atlas-shop-gradle")).unwrap();
    assert_eq!(facts.build_system, BuildSystem::Gradle);
    assert_eq!(facts.name, "atlas-shop");
    let ids: Vec<&str> = facts.modules.iter().map(|m| m.id.as_str()).collect();
    assert_eq!(ids, [":", ":shop-core", ":shop-catalog", ":shop-web"]);

    let catalog = facts.module(":shop-catalog").unwrap();
    assert_eq!(catalog.module_deps.len(), 1);
    assert!(catalog.module_deps[0].exposed, "api(project(...))");
    // The commented-out "ghost" dependencies must not appear.
    assert_eq!(catalog.external_deps.len(), 1);

    let web = facts.module(":shop-web").unwrap();
    let artifacts: Vec<&str> = web
        .external_deps
        .iter()
        .map(|d| d.artifact.as_str())
        .collect();
    assert_eq!(
        artifacts,
        ["javalin", "slf4j-api", "slf4j-simple", "junit-jupiter"]
    );

    // The root script uses `subprojects { }`, which we can only warn about.
    assert_eq!(facts.warnings.len(), 1);
    assert!(facts.warnings[0].message.contains("subprojects"));
}

#[test]
fn an_empty_folder_is_not_a_java_project() {
    let dir = std::env::temp_dir().join(format!("java-atlas-empty-{}", std::process::id()));
    std::fs::create_dir_all(&dir).unwrap();
    let result = read_build(&dir);
    std::fs::remove_dir_all(&dir).ok();
    assert!(matches!(result, Err(ScanError::NotAJavaProject { .. })));
}
