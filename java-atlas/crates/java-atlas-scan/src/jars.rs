//! Package lists of library jars found in local Maven/Gradle caches.
//!
//! Source code says `import org.slf4j.Logger`, the build file says
//! `org.slf4j:slf4j-api`. Which library provides which package is only
//! certain when we look inside the jar. We never download anything: if the
//! developer has built the project once, the jars are already on disk.

use std::collections::{BTreeSet, HashSet};
use std::fs::{self, File};
use std::path::{Path, PathBuf};

use java_atlas_core::facts::{LibraryIndex, ProjectFacts, ScanWarning};

/// A local artifact cache and its folder layout.
#[derive(Debug, Clone, PartialEq, Eq)]
pub enum LibraryCache {
    /// `~/.m2/repository/<group as path>/<artifact>/<version>/<artifact>-<version>.jar`
    Maven(PathBuf),
    /// `~/.gradle/caches/modules-2/files-2.1/<group>/<artifact>/<version>/<hash>/<artifact>-<version>.jar`
    Gradle(PathBuf),
}

/// Knobs of a scan.
#[derive(Debug, Clone, Default)]
pub struct ScanOptions {
    /// Where to look for library jars. Empty: do not look (deterministic tests).
    pub library_caches: Vec<LibraryCache>,
}

impl ScanOptions {
    /// The usual cache locations of the current user, if they exist.
    pub fn with_default_caches() -> Self {
        let home = std::env::var_os("HOME")
            .or_else(|| std::env::var_os("USERPROFILE"))
            .map(PathBuf::from);
        let gradle_home = std::env::var_os("GRADLE_USER_HOME")
            .map(PathBuf::from)
            .or_else(|| home.as_ref().map(|h| h.join(".gradle")));

        let mut library_caches = Vec::new();
        if let Some(home) = &home {
            library_caches.push(LibraryCache::Maven(home.join(".m2").join("repository")));
        }
        if let Some(gradle) = gradle_home {
            library_caches.push(LibraryCache::Gradle(
                gradle.join("caches").join("modules-2").join("files-2.1"),
            ));
        }
        library_caches.retain(|c| match c {
            LibraryCache::Maven(p) | LibraryCache::Gradle(p) => p.is_dir(),
        });
        ScanOptions { library_caches }
    }
}

fn find_jar(cache: &LibraryCache, group: &str, artifact: &str, version: &str) -> Option<PathBuf> {
    let file_name = format!("{artifact}-{version}.jar");
    match cache {
        LibraryCache::Maven(root) => {
            let mut dir = root.clone();
            dir.extend(group.split('.'));
            let jar = dir.join(artifact).join(version).join(file_name);
            jar.is_file().then_some(jar)
        }
        LibraryCache::Gradle(root) => {
            // One sub folder per file hash; the jar sits in one of them.
            let version_dir = root.join(group).join(artifact).join(version);
            fs::read_dir(version_dir)
                .ok()?
                .filter_map(Result::ok)
                .map(|entry| entry.path().join(&file_name))
                .find(|jar| jar.is_file())
        }
    }
}

/// The packages of the `.class` files inside a jar. Only the zip's central
/// directory (the table of contents at the end of the file) is read.
pub fn packages_in_jar(jar: &Path) -> Result<Vec<String>, String> {
    let file = File::open(jar).map_err(|e| e.to_string())?;
    let archive = zip::ZipArchive::new(file).map_err(|e| e.to_string())?;
    let packages: BTreeSet<String> = archive
        .file_names()
        // zip 9 reports names it cannot decode as errors; such entries are skipped.
        .filter_map(Result::ok)
        .filter(|name| name.ends_with(".class") && !name.starts_with("META-INF/"))
        .filter_map(|name| name.rsplit_once('/').map(|(dir, _)| dir.replace('/', ".")))
        .collect();
    Ok(packages.into_iter().collect())
}

/// Fills `facts.libraries` for every declared library whose jar is cached.
pub fn index_libraries(facts: &mut ProjectFacts, options: &ScanOptions) {
    if options.library_caches.is_empty() {
        return;
    }
    let mut seen = HashSet::new();
    let wanted: Vec<(String, String, String)> = facts
        .modules
        .iter()
        .flat_map(|m| &m.external_deps)
        .filter_map(|d| {
            d.version
                .clone()
                .map(|v| (d.group.clone(), d.artifact.clone(), v))
        })
        .filter(|key| seen.insert(key.clone()))
        .collect();

    for (group, artifact, version) in wanted {
        let Some(jar) = options
            .library_caches
            .iter()
            .find_map(|c| find_jar(c, &group, &artifact, &version))
        else {
            continue;
        };
        match packages_in_jar(&jar) {
            Ok(packages) => facts.libraries.push(LibraryIndex {
                group,
                artifact,
                version,
                jar: jar.display().to_string(),
                packages,
            }),
            Err(e) => facts.warnings.push(ScanWarning {
                file: jar.display().to_string(),
                message: format!("jar could not be read: {e}"),
            }),
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use std::io::Write;

    /// Writes a tiny jar (a zip) with the given entries; contents are irrelevant.
    pub(crate) fn write_jar(path: &Path, entries: &[&str]) {
        fs::create_dir_all(path.parent().unwrap()).unwrap();
        let mut zip = zip::ZipWriter::new(File::create(path).unwrap());
        let options = zip::write::SimpleFileOptions::default()
            .compression_method(zip::CompressionMethod::Stored);
        for entry in entries {
            zip.start_file(*entry, options).unwrap();
            zip.write_all(b"\xca\xfe\xba\xbe").unwrap();
        }
        zip.finish().unwrap();
    }

    #[test]
    fn packages_come_from_class_entries_only() {
        let dir = std::env::temp_dir().join(format!("java-atlas-jar-{}", std::process::id()));
        let jar = dir.join("x.jar");
        write_jar(
            &jar,
            &[
                "META-INF/MANIFEST.MF",
                "META-INF/versions/9/module-info.class",
                "org/slf4j/Logger.class",
                "org/slf4j/LoggerFactory.class",
                "org/slf4j/spi/LoggerFactoryBinder.class",
                "org/slf4j/messages.properties",
            ],
        );
        let packages = packages_in_jar(&jar).unwrap();
        fs::remove_dir_all(&dir).ok();
        assert_eq!(packages, ["org.slf4j", "org.slf4j.spi"]);
    }

    #[test]
    fn both_cache_layouts_are_searched() {
        let dir = std::env::temp_dir().join(format!("java-atlas-caches-{}", std::process::id()));
        let maven = dir.join("m2");
        let gradle = dir.join("gradle");
        write_jar(
            &maven.join("org/slf4j/slf4j-api/2.0.16/slf4j-api-2.0.16.jar"),
            &["org/slf4j/Logger.class"],
        );
        write_jar(
            &gradle.join("io.javalin/javalin/6.4.0/0a1b2c/javalin-6.4.0.jar"),
            &["io/javalin/Javalin.class"],
        );

        let m = find_jar(
            &LibraryCache::Maven(maven),
            "org.slf4j",
            "slf4j-api",
            "2.0.16",
        );
        let g = find_jar(
            &LibraryCache::Gradle(gradle.clone()),
            "io.javalin",
            "javalin",
            "6.4.0",
        );
        let missing = find_jar(
            &LibraryCache::Gradle(gradle),
            "io.javalin",
            "javalin",
            "9.9.9",
        );
        fs::remove_dir_all(&dir).ok();
        assert!(m.is_some());
        assert!(g.is_some_and(|p| p.ends_with("javalin-6.4.0.jar")));
        assert!(missing.is_none());
    }
}
