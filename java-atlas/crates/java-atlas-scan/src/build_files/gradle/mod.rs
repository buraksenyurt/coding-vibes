//! Gradle: settings + build scripts + version catalog → [`ProjectFacts`].
//!
//! Gradle scripts are programs (Groovy or Kotlin), so a complete reading
//! would mean running Gradle. We read them *heuristically* instead:
//!
//! - `settings.gradle(.kts)`: `rootProject.name = "…"` and `include(…)`.
//! - `build.gradle(.kts)`: the top-level `dependencies { … }` block, one
//!   `configuration(notation)` call at a time.
//! - `gradle/libs.versions.toml`: the version catalog behind `libs.…`.
//!
//! Anything dynamic (`subprojects { }`, loops, variables, convention plugins)
//! is invisible and reported as a warning where we can spot it.

mod catalog;
mod lexer;

use std::collections::HashMap;
use std::fs;
use std::path::{Path, PathBuf};

use java_atlas_core::facts::{
    BuildSystem, ExternalDep, ModuleDep, ModuleFacts, ProjectFacts, ScanWarning, Scope,
    VersionSource,
};

use crate::ScanError;
use crate::paths::{normalize, relative_slash};
use catalog::{Catalog, parse_catalog};
use lexer::{Tok, Token, lex};

// ---------------------------------------------------------------------------
// Reading scripts
// ---------------------------------------------------------------------------

/// What follows a configuration name.
#[derive(Debug, Clone, PartialEq, Eq)]
pub(crate) enum Notation {
    /// `"group:artifact:version"`
    Coordinates(String),
    /// `project(":core")`
    Project(String),
    /// `libs.jackson.databind`
    Catalog(String),
    /// `libs.bundles.logging`
    Bundle(String),
    /// Understood but deliberately not a library (`platform(…)`, `files(…)`).
    Ignored(&'static str),
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub(crate) struct Declaration {
    pub configuration: String,
    pub notation: Notation,
    pub line: u32,
}

/// Maps a configuration name to (scope, exposed-to-consumers).
fn configuration(name: &str) -> Option<(Scope, bool)> {
    Some(match name {
        "implementation" | "compile" => (Scope::Compile, false),
        "api" => (Scope::Compile, true),
        "compileOnly" | "annotationProcessor" => (Scope::Provided, false),
        "compileOnlyApi" => (Scope::Provided, true),
        "runtimeOnly" | "runtime" => (Scope::Runtime, false),
        "testImplementation"
        | "testCompileOnly"
        | "testRuntimeOnly"
        | "testAnnotationProcessor"
        | "testCompile"
        | "testRuntime" => (Scope::Test, false),
        _ => return None,
    })
}

fn ident(tokens: &[Token], i: usize) -> Option<&str> {
    match tokens.get(i).map(|t| &t.tok) {
        Some(Tok::Ident(s)) => Some(s),
        _ => None,
    }
}

fn string(tokens: &[Token], i: usize) -> Option<&str> {
    match tokens.get(i).map(|t| &t.tok) {
        Some(Tok::Str(s)) => Some(s),
        _ => None,
    }
}

fn is_sym(tokens: &[Token], i: usize, c: char) -> bool {
    matches!(tokens.get(i).map(|t| &t.tok), Some(Tok::Sym(s)) if *s == c)
}

/// Index just past the `}` that closes the `{` at `open`.
fn matching_brace(tokens: &[Token], open: usize) -> usize {
    let mut depth = 0usize;
    for (k, t) in tokens.iter().enumerate().skip(open) {
        match t.tok {
            Tok::Sym('{') => depth += 1,
            Tok::Sym('}') => {
                depth -= 1;
                if depth == 0 {
                    return k + 1;
                }
            }
            _ => {}
        }
    }
    tokens.len()
}

/// Same as [`matching_brace`] for parentheses.
fn matching_paren(tokens: &[Token], open: usize) -> usize {
    let mut depth = 0usize;
    for (k, t) in tokens.iter().enumerate().skip(open) {
        match t.tok {
            Tok::Sym('(') => depth += 1,
            Tok::Sym(')') => {
                depth -= 1;
                if depth == 0 {
                    return k + 1;
                }
            }
            _ => {}
        }
    }
    tokens.len()
}

/// Reads one notation starting at `*i`; on success `*i` is moved past it.
fn notation(tokens: &[Token], i: &mut usize) -> Option<Notation> {
    let start = *i;
    // Groovy and Kotlin both allow optional parentheses around the argument.
    let parenthesised = is_sym(tokens, start, '(');
    let at = if parenthesised { start + 1 } else { start };

    let (found, end) = match (ident(tokens, at), string(tokens, at)) {
        (_, Some(s)) => (Notation::Coordinates(s.to_owned()), at + 1),

        (Some("project"), _) => {
            // project(":x")  |  project(path: ":x")  |  project ':x'
            let (inner, end) = if is_sym(tokens, at + 1, '(') {
                (at + 2, matching_paren(tokens, at + 1))
            } else {
                (at + 1, at + 2)
            };
            let path = string(tokens, inner).or_else(|| {
                (ident(tokens, inner) == Some("path")
                    && matches!(
                        tokens.get(inner + 1).map(|t| &t.tok),
                        Some(Tok::Sym(':' | '='))
                    ))
                .then(|| string(tokens, inner + 2))
                .flatten()
            })?;
            (Notation::Project(path.to_owned()), end)
        }

        (Some("libs"), _) => {
            // libs . a . b . c
            let mut parts = Vec::new();
            let mut k = at + 1;
            while is_sym(tokens, k, '.') {
                parts.push(ident(tokens, k + 1)?.to_owned());
                k += 2;
            }
            let found = match parts.split_first() {
                Some((first, rest)) if first == "bundles" => Notation::Bundle(rest.join(".")),
                Some((first, _)) if first == "plugins" || first == "versions" => {
                    Notation::Ignored("catalog plugin/version")
                }
                Some(_) => Notation::Catalog(parts.join(".")),
                None => return None,
            };
            (found, k)
        }

        (Some("platform" | "enforcedPlatform"), _) if is_sym(tokens, at + 1, '(') => (
            Notation::Ignored("platform/BOM"),
            matching_paren(tokens, at + 1),
        ),
        (Some("files" | "fileTree"), _) if is_sym(tokens, at + 1, '(') => (
            Notation::Ignored("local files"),
            matching_paren(tokens, at + 1),
        ),
        (Some("testFixtures"), _) if is_sym(tokens, at + 1, '(') => {
            let mut inner = at + 2;
            let found = notation(tokens, &mut inner)?;
            (found, matching_paren(tokens, at + 1))
        }

        // group: 'g', name: 'a', version: 'v'   (Groovy map; `=` in Kotlin named args)
        (Some("group"), _) => {
            let mut fields: HashMap<&str, &str> = HashMap::new();
            let mut k = at;
            while let (Some(key), Some(value)) = (ident(tokens, k), string(tokens, k + 2)) {
                if !matches!(tokens.get(k + 1).map(|t| &t.tok), Some(Tok::Sym(':' | '='))) {
                    break;
                }
                fields.insert(key, value);
                k += 3;
                if is_sym(tokens, k, ',') {
                    k += 1;
                } else {
                    break;
                }
            }
            let coords = match (
                fields.get("group"),
                fields.get("name"),
                fields.get("version"),
            ) {
                (Some(g), Some(n), Some(v)) => format!("{g}:{n}:{v}"),
                (Some(g), Some(n), None) => format!("{g}:{n}"),
                _ => return None,
            };
            (Notation::Coordinates(coords), k)
        }

        _ => return None,
    };

    *i = if parenthesised && is_sym(tokens, end, ')') {
        end + 1
    } else {
        end
    };
    Some(found)
}

/// Finds every `configuration(notation)` inside top-level `dependencies { }` blocks.
/// Returns the declarations and the lines that could not be understood.
pub(crate) fn declarations(tokens: &[Token]) -> (Vec<Declaration>, Vec<u32>) {
    let mut found = Vec::new();
    let mut unknown = Vec::new();
    let mut depth = 0usize;
    let mut i = 0;

    while i < tokens.len() {
        match &tokens[i].tok {
            Tok::Sym('{') => depth += 1,
            Tok::Sym('}') => depth = depth.saturating_sub(1),
            Tok::Ident(name)
                if depth == 0 && name == "dependencies" && is_sym(tokens, i + 1, '{') =>
            {
                let end = matching_brace(tokens, i + 1);
                read_block(
                    &tokens[i + 2..end.saturating_sub(1)],
                    &mut found,
                    &mut unknown,
                );
                i = end;
                continue;
            }
            _ => {}
        }
        i += 1;
    }
    (found, unknown)
}

fn read_block(tokens: &[Token], found: &mut Vec<Declaration>, unknown: &mut Vec<u32>) {
    let mut i = 0;
    while i < tokens.len() {
        // A closure after a dependency (`{ exclude … }`) or a `constraints { }` block.
        if is_sym(tokens, i, '{') {
            i = matching_brace(tokens, i);
            continue;
        }
        let Some(name) = ident(tokens, i) else {
            i += 1;
            continue;
        };
        if configuration(name).is_none() {
            i += 1;
            continue;
        }
        let line = tokens[i].line;
        let mut k = i + 1;
        let mut any = false;
        // Groovy allows several notations: implementation 'a:b:1', 'c:d:2'
        while let Some(n) = notation(tokens, &mut k) {
            found.push(Declaration {
                configuration: name.to_owned(),
                notation: n,
                line,
            });
            any = true;
            if is_sym(tokens, k, ',') {
                k += 1;
            } else {
                break;
            }
        }
        if !any {
            unknown.push(line);
        }
        i = k.max(i + 1);
    }
}

/// `rootProject.name` and the `include`d project paths of a settings script.
pub(crate) fn settings(tokens: &[Token]) -> (Option<String>, Vec<String>) {
    let mut name = None;
    let mut includes = Vec::new();
    for i in 0..tokens.len() {
        match ident(tokens, i) {
            Some("rootProject")
                if is_sym(tokens, i + 1, '.')
                    && ident(tokens, i + 2) == Some("name")
                    && is_sym(tokens, i + 3, '=') =>
            {
                name = string(tokens, i + 4).map(str::to_owned);
            }
            Some("include") => {
                let mut k = if is_sym(tokens, i + 1, '(') {
                    i + 2
                } else {
                    i + 1
                };
                while let Some(path) = string(tokens, k) {
                    includes.push(project_path(path));
                    k += 1;
                    if is_sym(tokens, k, ',') {
                        k += 1;
                    } else {
                        break;
                    }
                }
            }
            _ => {}
        }
    }
    (name, includes)
}

/// `shop-core` / `:shop-core` → `:shop-core`.
fn project_path(raw: &str) -> String {
    if raw.starts_with(':') {
        raw.to_owned()
    } else {
        format!(":{raw}")
    }
}

// ---------------------------------------------------------------------------
// Putting it together
// ---------------------------------------------------------------------------

fn read_text(path: &Path) -> Result<String, ScanError> {
    fs::read_to_string(path).map_err(|source| ScanError::Io {
        path: path.display().to_string(),
        source,
    })
}

fn first_existing(dir: &Path, names: &[&str]) -> Option<PathBuf> {
    names.iter().map(|n| dir.join(n)).find(|p| p.is_file())
}

struct Project {
    id: String,
    name: String,
    dir: PathBuf,
}

/// Reads a Gradle project rooted at `root`.
pub fn read(root: &Path) -> Result<ProjectFacts, ScanError> {
    let root = normalize(root);
    let mut warnings = Vec::new();
    let mut warn = |file: &Path, message: String| {
        warnings.push(ScanWarning {
            file: relative_slash(&root, file),
            message,
        });
    };

    // 1. settings
    let folder_name = root
        .file_name()
        .map(|n| n.to_string_lossy().into_owned())
        .unwrap_or_else(|| "project".to_owned());
    let (root_name, includes) =
        match first_existing(&root, &["settings.gradle.kts", "settings.gradle"]) {
            Some(path) => settings(&lex(&read_text(&path)?)),
            None => (None, Vec::new()),
        };
    let root_name = root_name.unwrap_or(folder_name);

    // 2. version catalog
    let catalog_path = root.join("gradle").join("libs.versions.toml");
    let catalog = if catalog_path.is_file() {
        let (catalog, skipped) =
            parse_catalog(&read_text(&catalog_path)?).map_err(|message| ScanError::Toml {
                path: catalog_path.display().to_string(),
                message,
            })?;
        for alias in skipped {
            warn(
                &catalog_path,
                format!("library '{alias}' could not be read"),
            );
        }
        catalog
    } else {
        Catalog::default()
    };

    // 3. projects: the root plus every include, in declaration order
    let mut projects = vec![Project {
        id: ":".into(),
        name: root_name.clone(),
        dir: root.clone(),
    }];
    for path in &includes {
        let segments: Vec<&str> = path.trim_start_matches(':').split(':').collect();
        let dir = segments.iter().fold(root.clone(), |d, s| d.join(s));
        let name = segments.last().copied().unwrap_or_default().to_owned();
        projects.push(Project {
            id: path.clone(),
            name,
            dir,
        });
    }
    let known: Vec<String> = projects.iter().map(|p| p.id.clone()).collect();

    // 4. one build script per project
    let mut modules = Vec::with_capacity(projects.len());
    for project in &projects {
        let script = first_existing(&project.dir, &["build.gradle.kts", "build.gradle"]);
        let is_root = project.id == ":";
        let mut module = ModuleFacts {
            id: project.id.clone(),
            name: project.name.clone(),
            path: relative_slash(&root, &project.dir),
            build_file: script
                .as_deref()
                .map(|s| relative_slash(&root, s))
                .unwrap_or_default(),
            // The root only groups the others when it has no sources of its own.
            aggregator: is_root && !includes.is_empty() && !project.dir.join("src").is_dir(),
            source_roots: Vec::new(),
            jpms: None,
            files: Vec::new(),
            module_deps: Vec::new(),
            external_deps: Vec::new(),
        };

        let Some(script) = script else {
            if !project.dir.is_dir() {
                warn(
                    &project.dir,
                    format!("included project {} has no folder", project.id),
                );
            }
            modules.push(module);
            continue;
        };
        let tokens = lex(&read_text(&script)?);

        for block in ["subprojects", "allprojects"] {
            if tokens.iter().any(|t| t.tok == Tok::Ident(block.into())) {
                warn(
                    &script,
                    format!("`{block} {{ }}` is not applied to other projects"),
                );
            }
        }

        let (decls, unknown) = declarations(&tokens);
        for line in unknown {
            warn(
                &script,
                format!("line {line}: dependency notation not understood"),
            );
        }

        for d in decls {
            let Some((scope, exposed)) = configuration(&d.configuration) else {
                continue;
            };
            let line = d.line;
            match d.notation {
                Notation::Ignored(_) => {}
                Notation::Project(path) => {
                    let target = project_path(&path);
                    if known.contains(&target) {
                        module.module_deps.push(ModuleDep {
                            target,
                            scope,
                            exposed,
                        });
                    } else {
                        warn(
                            &script,
                            format!("line {line}: project {target} is not included in settings"),
                        );
                    }
                }
                Notation::Coordinates(s) => match coordinates(&s, scope) {
                    Some(dep) => {
                        if dep.version_source == VersionSource::Unresolved {
                            warn(
                                &script,
                                format!(
                                    "line {line}: {} has no literal version",
                                    dep.coordinates()
                                ),
                            );
                        }
                        module.external_deps.push(dep);
                    }
                    None => warn(
                        &script,
                        format!("line {line}: '{s}' is not group:artifact[:version]"),
                    ),
                },
                Notation::Catalog(acc) => match from_catalog(&catalog, &acc, scope) {
                    Some(dep) => module.external_deps.push(dep),
                    None => warn(
                        &script,
                        format!("line {line}: libs.{acc} is not in the version catalog"),
                    ),
                },
                Notation::Bundle(bundle) => match catalog.bundles.get(&bundle) {
                    Some(members) => {
                        for acc in members {
                            match from_catalog(&catalog, acc, scope) {
                                Some(dep) => module.external_deps.push(dep),
                                None => warn(
                                    &script,
                                    format!(
                                        "line {line}: bundle member {acc} is not in the catalog"
                                    ),
                                ),
                            }
                        }
                    }
                    None => warn(
                        &script,
                        format!("line {line}: libs.bundles.{bundle} is not in the version catalog"),
                    ),
                },
            }
        }
        modules.push(module);
    }

    Ok(ProjectFacts {
        name: root_name,
        build_system: BuildSystem::Gradle,
        modules,
        warnings,
        libraries: Vec::new(),
    })
}

fn coordinates(s: &str, scope: Scope) -> Option<ExternalDep> {
    let mut parts = s.split(':');
    let group = parts.next().filter(|g| !g.is_empty())?.to_owned();
    let artifact = parts.next().filter(|a| !a.is_empty())?.to_owned();
    // A `$variable` or `${…}` template cannot be evaluated without Gradle.
    let version = parts
        .next()
        .filter(|v| !v.is_empty() && !v.contains('$'))
        .map(str::to_owned);
    let version_source = if version.is_some() {
        VersionSource::Explicit
    } else {
        VersionSource::Unresolved
    };
    Some(ExternalDep {
        group,
        artifact,
        version,
        version_source,
        scope,
        inherited: false,
    })
}

fn from_catalog(catalog: &Catalog, acc: &str, scope: Scope) -> Option<ExternalDep> {
    let lib = catalog.libraries.get(acc)?;
    Some(ExternalDep {
        group: lib.group.clone(),
        artifact: lib.artifact.clone(),
        version: lib.version.clone(),
        version_source: VersionSource::Catalog(acc.to_owned()),
        scope,
        inherited: false,
    })
}

#[cfg(test)]
mod tests {
    use super::*;

    fn decls(src: &str) -> Vec<(String, Notation)> {
        declarations(&lex(src))
            .0
            .into_iter()
            .map(|d| (d.configuration, d.notation))
            .collect()
    }

    #[test]
    fn kotlin_and_groovy_notations() {
        let src = r#"
            dependencies {
                implementation("a:b:1")
                api project(':core')
                implementation(project(path = ":util"))
                testImplementation libs.junit.jupiter
                runtimeOnly(libs.bundles.logging)
                compileOnly group: 'g', name: 'n', version: '2'
                implementation 'x:y:1', 'z:w:2'
            }"#;
        let c = |s: &str| Notation::Coordinates(s.into());
        assert_eq!(
            decls(src),
            vec![
                ("implementation".into(), c("a:b:1")),
                ("api".into(), Notation::Project(":core".into())),
                ("implementation".into(), Notation::Project(":util".into())),
                (
                    "testImplementation".into(),
                    Notation::Catalog("junit.jupiter".into())
                ),
                ("runtimeOnly".into(), Notation::Bundle("logging".into())),
                ("compileOnly".into(), c("g:n:2")),
                ("implementation".into(), c("x:y:1")),
                ("implementation".into(), c("z:w:2")),
            ]
        );
    }

    #[test]
    fn nested_dependencies_blocks_are_not_ours() {
        let src = r#"
            subprojects { dependencies { implementation("ghost:ghost:1") } }
            dependencies { implementation("real:real:1") }"#;
        assert_eq!(
            decls(src),
            vec![(
                "implementation".into(),
                Notation::Coordinates("real:real:1".into())
            )]
        );
    }

    #[test]
    fn closures_and_platforms_are_skipped() {
        let src = r#"
            dependencies {
                implementation(platform("org.springframework.boot:spring-boot-dependencies:3.3.0"))
                implementation("a:b:1") { exclude(group = "c") }
                constraints { implementation("pinned:only:9") }
            }"#;
        let found = decls(src);
        assert_eq!(found.len(), 2);
        assert_eq!(found[0].1, Notation::Ignored("platform/BOM"));
        assert_eq!(found[1].1, Notation::Coordinates("a:b:1".into()));
    }

    #[test]
    fn variables_are_reported_as_not_understood() {
        let (found, unknown) = declarations(&lex("dependencies {\n  implementation(myLib)\n}"));
        assert!(found.is_empty());
        assert_eq!(unknown, vec![2]);
    }

    #[test]
    fn settings_in_both_dialects() {
        let kts = settings(&lex(r#"rootProject.name = "shop"
            include("a", ":b")
            include(":c:d")"#));
        assert_eq!(
            kts,
            (
                Some("shop".into()),
                vec![":a".into(), ":b".into(), ":c:d".into()]
            )
        );

        let groovy = settings(&lex("rootProject.name = 'shop'\ninclude 'a', 'b'"));
        assert_eq!(groovy.1, vec![":a", ":b"]);
    }

    #[test]
    fn templated_versions_are_unresolved() {
        let dep = coordinates("g:a:$ver", Scope::Compile).unwrap();
        assert_eq!(dep.version, None);
        assert_eq!(dep.version_source, VersionSource::Unresolved);
    }
}
