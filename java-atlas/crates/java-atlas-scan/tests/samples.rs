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

// ---------------------------------------------------------------------------
// Phase 2: sources
// ---------------------------------------------------------------------------

mod sources {
    use super::*;
    use java_atlas_core::facts::{SourceFileFacts, TypeKind};
    use java_atlas_scan::{Progress, read_project};
    use std::sync::Mutex;

    fn file<'a>(
        facts: &'a java_atlas_core::facts::ProjectFacts,
        suffix: &str,
    ) -> &'a SourceFileFacts {
        facts
            .modules
            .iter()
            .flat_map(|m| &m.files)
            .find(|f| f.path.ends_with(suffix))
            .unwrap_or_else(|| panic!("no file ending with {suffix}"))
    }

    #[test]
    fn atlas_bank_sources_snapshot() {
        let facts = read_project(&sample("atlas-bank"), &|_| {}).unwrap();
        insta::assert_snapshot!(facts.describe_sources());
    }

    #[test]
    fn atlas_shop_sources_snapshot() {
        let facts = read_project(&sample("atlas-shop-gradle"), &|_| {}).unwrap();
        insta::assert_snapshot!(facts.describe_sources());
    }

    #[test]
    fn every_file_lands_in_its_module() {
        let facts = read_project(&sample("atlas-bank"), &|_| {}).unwrap();
        let count = |id: &str| facts.module(id).unwrap().files.len();
        assert_eq!(
            count("com.atlasbank:atlas-bank"),
            0,
            "aggregators have no sources"
        );
        assert_eq!(count("com.atlasbank:bank-common"), 2);
        assert_eq!(count("com.atlasbank:bank-domain"), 4);
        assert_eq!(count("com.atlasbank:bank-persistence"), 3);
        assert_eq!(count("com.atlasbank:bank-service"), 6);
        assert_eq!(count("com.atlasbank:bank-api"), 2);
        assert_eq!(
            facts.file_count(),
            17,
            "module-info.java files are not counted as files"
        );
        assert!(facts.warnings.is_empty(), "{:?}", facts.warnings);
    }

    #[test]
    fn jpms_is_read_only_where_declared() {
        let facts = read_project(&sample("atlas-bank"), &|_| {}).unwrap();
        let jpms = |id: &str| facts.module(id).unwrap().jpms.as_ref();

        let domain = jpms("com.atlasbank:bank-domain").unwrap();
        assert_eq!(domain.name, "com.atlasbank.domain");
        assert!(domain.requires[0].transitive);
        let persistence = jpms("com.atlasbank:bank-persistence").unwrap();
        let exported: Vec<&str> = persistence
            .exports
            .iter()
            .map(|e| e.package.as_str())
            .collect();
        assert_eq!(
            exported,
            ["com.atlasbank.persistence"],
            "internal is not exported"
        );

        assert!(jpms("com.atlasbank:bank-service").is_none());
        assert!(jpms("com.atlasbank:bank-api").is_none());
    }

    #[test]
    fn the_tricky_cases_are_all_captured() {
        let facts = read_project(&sample("atlas-bank"), &|_| {}).unwrap();

        // Wildcard import
        let service = file(&facts, "AccountService.java");
        assert!(
            service
                .imports
                .iter()
                .any(|i| i.path == "com.atlasbank.domain.model" && i.on_demand)
        );
        // Static import
        let transfer = file(&facts, "TransferService.java");
        assert!(
            transfer
                .imports
                .iter()
                .any(|i| i.is_static && i.path == "com.atlasbank.common.Money.zero")
        );
        // Same-package use without an import: AccountService appears as a reference.
        assert!(
            transfer
                .references
                .iter()
                .any(|r| r.name == "AccountService")
        );
        // Nested type import
        let summary = file(&facts, "AccountSummary.java");
        assert!(
            summary
                .imports
                .iter()
                .any(|i| i.path == "com.atlasbank.domain.model.Account.Status")
        );
        // Fully qualified name without an import
        let store = file(&facts, "InMemoryAccountStore.java");
        assert!(
            store
                .references
                .iter()
                .any(|r| r.name == "java.util.concurrent.atomic.AtomicLong" && r.count == 2)
        );
        // Member type declaration
        let account = file(&facts, "domain/model/Account.java");
        let kinds: Vec<(&str, TypeKind)> = account
            .types
            .iter()
            .map(|t| (t.name.as_str(), t.kind))
            .collect();
        assert_eq!(
            kinds,
            [
                ("Account", TypeKind::Class),
                ("Account.Status", TypeKind::Enum)
            ]
        );
        // Test sources are marked
        assert!(file(&facts, "AccountSummaryTest.java").is_test);
    }

    #[test]
    fn progress_reaches_the_total() {
        let seen = Mutex::new(Vec::new());
        let facts = read_project(&sample("atlas-shop-gradle"), &|p: Progress| {
            seen.lock().unwrap().push(p)
        })
        .unwrap();
        let seen = seen.into_inner().unwrap();
        let total = facts.file_count();
        assert_eq!(seen.first(), Some(&Progress { done: 0, total }));
        assert_eq!(seen.iter().map(|p| p.done).max(), Some(total));
        assert_eq!(seen.len(), total + 1);
    }
}

/// Guava keeps its sources in `src/` and `test/`, declared once in the parent
/// POM. The folders are inherited but resolved per module.
#[test]
fn inherited_source_directories_are_honoured() {
    use java_atlas_core::facts::SourceRoot;
    use std::fs;

    let root = std::env::temp_dir().join(format!("java-atlas-srcdir-{}", std::process::id()));
    let _ = fs::remove_dir_all(&root);
    fs::create_dir_all(root.join("lib/src/p")).unwrap();
    fs::create_dir_all(root.join("lib/test/p")).unwrap();
    fs::write(
        root.join("pom.xml"),
        "<project><groupId>g</groupId><artifactId>parent</artifactId><version>1</version>\
         <packaging>pom</packaging><modules><module>lib</module></modules>\
         <build><sourceDirectory>${project.basedir}/src</sourceDirectory>\
         <testSourceDirectory>test</testSourceDirectory></build></project>",
    )
    .unwrap();
    fs::write(
        root.join("lib/pom.xml"),
        "<project><parent><groupId>g</groupId><artifactId>parent</artifactId><version>1</version></parent>\
         <artifactId>lib</artifactId></project>",
    )
    .unwrap();
    fs::write(root.join("lib/src/p/A.java"), "package p; class A {}").unwrap();
    fs::write(
        root.join("lib/test/p/ATest.java"),
        "package p; class ATest {}",
    )
    .unwrap();

    let facts = java_atlas_scan::read_project(&root, &|_| {}).unwrap();
    fs::remove_dir_all(&root).ok();

    let lib = facts.module("g:lib").unwrap();
    assert_eq!(
        lib.source_roots,
        [
            SourceRoot {
                path: "src".into(),
                is_test: false
            },
            SourceRoot {
                path: "test".into(),
                is_test: true
            },
        ]
    );
    let files: Vec<(&str, bool)> = lib
        .files
        .iter()
        .map(|f| (f.path.as_str(), f.is_test))
        .collect();
    assert_eq!(
        files,
        [("lib/src/p/A.java", false), ("lib/test/p/ATest.java", true)]
    );
}
