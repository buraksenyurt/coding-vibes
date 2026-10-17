//! Phase 3 end to end: sample project → facts → atlas.

use std::path::PathBuf;

use java_atlas_core::atlas::{Atlas, FindingKind, Level, NodeKind, ViolationKind};
use java_atlas_scan::{LibraryCache, ScanOptions, read_project};

fn sample(name: &str) -> PathBuf {
    PathBuf::from(env!("CARGO_MANIFEST_DIR"))
        .join("../../samples")
        .join(name)
}

fn atlas(name: &str) -> Atlas {
    Atlas::build(&read_project(&sample(name), &ScanOptions::default(), &|_| {}).unwrap())
}

fn fqn(atlas: &Atlas, id: java_atlas_core::atlas::NodeId) -> &str {
    &atlas.node(id).fqn
}

#[test]
fn atlas_bank_snapshot() {
    insta::assert_snapshot!(atlas("atlas-bank").describe());
}

#[test]
fn atlas_shop_snapshot() {
    insta::assert_snapshot!(atlas("atlas-shop-gradle").describe());
}

#[test]
fn atlas_bank_has_the_planted_cycle_and_one_we_did_not_plan() {
    let a = atlas("atlas-bank");
    let cycles: Vec<Vec<&str>> = a
        .cycles
        .iter()
        .map(|c| c.packages.iter().map(|p| fqn(&a, *p)).collect())
        .collect();
    assert_eq!(
        cycles,
        [
            // Repositories → InMemoryAccountStore → AccountRepository: a classic
            // "factory in the API package" cycle that slipped into the sample.
            vec![
                "com.atlasbank.persistence",
                "com.atlasbank.persistence.internal"
            ],
            // The one planted on purpose.
            vec![
                "com.atlasbank.service.account",
                "com.atlasbank.service.audit"
            ],
        ]
    );
    let (from, to, weight) = a.cycles[1].weakest;
    assert_eq!(
        (fqn(&a, from), fqn(&a, to), weight),
        (
            "com.atlasbank.service.audit",
            "com.atlasbank.service.account",
            2
        )
    );
}

#[test]
fn atlas_bank_has_exactly_one_smuggling_route() {
    let a = atlas("atlas-bank");
    assert_eq!(a.violations.len(), 1);
    let v = &a.violations[0];
    assert_eq!(v.kind, ViolationKind::NotExported);
    assert_eq!(fqn(&a, v.from_module), "com.atlasbank:bank-api");
    assert_eq!(fqn(&a, v.to_package), "com.atlasbank.persistence.internal");
    assert_eq!(
        v.evidence[0].file,
        "bank-api/src/main/java/com/atlasbank/api/AccountController.java"
    );
    assert_eq!(v.evidence[0].line, 3);
}

#[test]
fn atlas_bank_build_file_findings() {
    let a = atlas("atlas-bank");
    let found: Vec<(String, &str, &str)> = a
        .findings
        .iter()
        .map(|f| {
            (
                format!("{:?}", f.kind),
                fqn(&a, f.module),
                fqn(&a, f.target),
            )
        })
        .collect();
    assert_eq!(
        found,
        [
            (
                "UnusedLibrary".into(),
                "com.atlasbank:bank-common",
                "org.apache.commons:commons-lang3"
            ),
            (
                format!(
                    "{:?}",
                    FindingKind::UndeclaredModuleDependency { reachable: true }
                ),
                "com.atlasbank:bank-service",
                "com.atlasbank:bank-common"
            ),
            (
                format!(
                    "{:?}",
                    FindingKind::UndeclaredModuleDependency { reachable: true }
                ),
                "com.atlasbank:bank-api",
                "com.atlasbank:bank-persistence"
            ),
        ]
    );
}

#[test]
fn atlas_bank_metrics() {
    let a = atlas("atlas-bank");
    let m = |name: &str| a.metrics(a.find(name).unwrap()).unwrap();
    // Everyone uses common, it uses nobody: maximally stable.
    assert_eq!(
        (m("com.atlasbank.common").ca, m("com.atlasbank.common").ce),
        (3, 0)
    );
    assert_eq!(m("com.atlasbank.common").instability(), Some(0.0));
    // Nobody uses the API package: maximally unstable, as an entry point should be.
    assert_eq!(m("com.atlasbank.api").instability(), Some(1.0));
}

#[test]
fn edges_add_up_from_types_to_modules() {
    let a = atlas("atlas-bank");
    let total = |level| a.edges(level).iter().map(|e| e.weight.total()).sum::<u32>();
    // Every use is counted once at type level; lifting can only drop uses
    // that stay inside one package / module.
    assert!(total(Level::Type) >= total(Level::Package));
    assert!(total(Level::Package) >= total(Level::Module));
    let jdk = a.nodes.iter().filter(|n| n.kind == NodeKind::Jdk).count();
    assert!(
        jdk >= 5,
        "java.lang, java.util, java.math, java.time, … found: {jdk}"
    );
}

#[test]
fn a_cached_jar_turns_a_guess_into_a_fact() {
    use std::fs::{self, File};
    use std::io::Write;

    let dir = std::env::temp_dir().join(format!("java-atlas-m2-{}", std::process::id()));
    let jar = dir.join("org/slf4j/slf4j-api/2.0.16/slf4j-api-2.0.16.jar");
    fs::create_dir_all(jar.parent().unwrap()).unwrap();
    let mut zip = zip::ZipWriter::new(File::create(&jar).unwrap());
    let stored =
        zip::write::SimpleFileOptions::default().compression_method(zip::CompressionMethod::Stored);
    zip.start_file("org/slf4j/Logger.class", stored).unwrap();
    zip.write_all(b"\xca\xfe\xba\xbe").unwrap();
    zip.finish().unwrap();

    let options = ScanOptions {
        library_caches: vec![LibraryCache::Maven(dir.clone())],
    };
    let facts = read_project(&sample("atlas-bank"), &options, &|_| {}).unwrap();
    fs::remove_dir_all(&dir).ok();

    assert_eq!(facts.libraries.len(), 1);
    assert_eq!(facts.libraries[0].packages, ["org.slf4j"]);
    let a = Atlas::build(&facts);
    let NodeKind::Library { packages, .. } = &a.node(a.find("org.slf4j:slf4j-api").unwrap()).kind
    else {
        panic!()
    };
    assert_eq!(
        packages,
        &[(
            "org.slf4j".to_owned(),
            java_atlas_core::atlas::MatchMethod::Jar
        )]
    );
}

// ---------------------------------------------------------------------------
// Phase 4: views
// ---------------------------------------------------------------------------

mod views {
    use super::*;
    use java_atlas_core::Scale;
    use java_atlas_core::atlas::ViewFilters;

    fn names(a: &Atlas, ids: &[java_atlas_core::atlas::NodeId]) -> Vec<String> {
        ids.iter().map(|id| a.node(*id).fqn.clone()).collect()
    }

    fn roads(a: &Atlas, v: &java_atlas_core::atlas::View) -> Vec<String> {
        v.edges
            .iter()
            .map(|e| {
                format!(
                    "{} → {} {}",
                    a.node(e.from).name,
                    a.node(e.to).name,
                    e.weight.main
                )
            })
            .collect()
    }

    #[test]
    fn country_shows_modules_and_every_island() {
        let a = atlas("atlas-bank");
        let v = a.view(Scale::Country, None, ViewFilters::default());
        assert_eq!(v.members.len(), 5);
        // commons-lang3 is unused but still an island; junit only has test roads.
        assert_eq!(
            names(&a, &v.neighbours),
            [
                "org.junit.jupiter:junit-jupiter",
                "org.apache.commons:commons-lang3",
                "org.slf4j:slf4j-api",
                "com.fasterxml.jackson.core:jackson-databind"
            ]
        );
        assert!(
            roads(&a, &v).iter().all(|r| !r.contains("junit")),
            "test roads are filtered out"
        );
    }

    #[test]
    fn region_folds_foreign_packages_into_their_module() {
        let a = atlas("atlas-bank");
        let api = a.find("com.atlasbank:bank-api").unwrap();
        let v = a.view(Scale::Region, Some(api), ViewFilters::default());
        assert_eq!(names(&a, &v.members), ["com.atlasbank.api"]);
        assert_eq!(
            roads(&a, &v),
            [
                // persistence.internal + … → the bank-persistence region at the border
                "com.atlasbank.api → bank-persistence 3",
                "com.atlasbank.api → bank-service 9",
                "com.atlasbank.api → slf4j-api 4",
                "com.atlasbank.api → jackson-databind 3",
            ]
        );
    }

    #[test]
    fn city_shows_buildings_and_incoming_roads() {
        let a = atlas("atlas-bank");
        let audit = a.find("com.atlasbank.service.audit").unwrap();
        let v = a.view(
            Scale::City,
            Some(audit),
            ViewFilters {
                tests: true,
                ..ViewFilters::default()
            },
        );
        assert_eq!(
            names(&a, &v.members),
            [
                "com.atlasbank.service.audit.AuditEntry",
                "com.atlasbank.service.audit.AuditLog"
            ]
        );
        let r = roads(&a, &v);
        assert!(r.contains(&"AuditLog → AuditEntry 3".to_owned()), "{r:?}");
        assert!(
            r.contains(&"AuditLog → com.atlasbank.service.account 2".to_owned()),
            "{r:?}"
        );
        assert!(
            r.contains(&"com.atlasbank.service.account → AuditLog 3".to_owned()),
            "incoming: {r:?}"
        );
    }

    #[test]
    fn keys_are_stable_and_unique() {
        let a = atlas("atlas-bank");
        let index = a.key_index();
        assert_eq!(index.len(), a.nodes.len());
        let package = a.find("com.atlasbank.common").unwrap();
        assert_eq!(
            a.key(package),
            "p:com.atlasbank:bank-common|com.atlasbank.common"
        );
        assert_eq!(index[&a.key(package)], package);
    }

    #[test]
    fn evidence_works_across_levels() {
        let a = atlas("atlas-bank");
        let api = a.find("com.atlasbank.api").unwrap();
        let persistence = a.find("com.atlasbank:bank-persistence").unwrap();
        let ev = a.evidence_between(api, persistence, 10);
        // The import (line 3) and the first use of the name (line 16). Line 17
        // uses it again, but facts keep one line per name and file.
        let lines: Vec<u32> = ev.iter().map(|e| e.line).collect();
        assert_eq!(lines, [3, 16]);
    }

    #[test]
    fn sizes_add_up() {
        let a = atlas("atlas-bank");
        let sizes = a.sizes();
        let module = a.find("com.atlasbank:bank-common").unwrap();
        let money = a.find("com.atlasbank.common.Money").unwrap();
        let ids = a.find("com.atlasbank.common.Ids").unwrap();
        assert_eq!(
            sizes[module.index()],
            sizes[money.index()] + sizes[ids.index()]
        );
    }
}
