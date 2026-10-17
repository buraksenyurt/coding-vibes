//! A plain-text report of the atlas, for the `inspect` example and snapshots.

use std::fmt::Write as _;

use super::{Atlas, FindingKind, Level, MatchMethod, NodeKind, ViolationKind};

impl Atlas {
    pub fn describe(&self) -> String {
        let mut out = String::new();
        let count =
            |pred: fn(&NodeKind) -> bool| self.nodes.iter().filter(|n| pred(&n.kind)).count();
        let _ = writeln!(
            out,
            "{} · {} module(s) · {} package(s) · {} type(s) · {} librar(y/ies)",
            self.name,
            count(|k| matches!(k, NodeKind::Module { .. })),
            count(|k| matches!(k, NodeKind::Package { .. })),
            count(|k| matches!(k, NodeKind::Type { .. })),
            count(|k| matches!(k, NodeKind::Library { .. })),
        );

        let label = |id| {
            let n = self.node(id);
            match &n.kind {
                NodeKind::Library { .. } => format!("⇢ {}", n.fqn),
                NodeKind::Jdk => format!("jdk {}", n.fqn),
                NodeKind::Unknown => format!("? {}", n.fqn),
                _ => n.fqn.clone(),
            }
        };

        for (title, level) in [
            ("module roads", Level::Module),
            ("package roads", Level::Package),
        ] {
            let _ = writeln!(out, "\n{title}:");
            for e in self.edges(level) {
                if matches!(self.node(e.to).kind, NodeKind::Jdk) {
                    continue; // the mainland is everyone's neighbour; keep the report short
                }
                let test = if e.weight.test > 0 {
                    format!(" +{} test", e.weight.test)
                } else {
                    String::new()
                };
                let first = e
                    .evidence
                    .first()
                    .map(|ev| format!("  ({}:{})", ev.file, ev.line))
                    .unwrap_or_default();
                let _ = writeln!(
                    out,
                    "  {} → {}  {}{test}{first}",
                    label(e.from),
                    label(e.to),
                    e.weight.main
                );
            }
        }

        let _ = writeln!(out, "\nislands:");
        for n in &self.nodes {
            if let NodeKind::Library {
                version, packages, ..
            } = &n.kind
            {
                let version = version.as_deref().unwrap_or("?");
                let matched: Vec<String> = packages
                    .iter()
                    .map(|(p, m)| match m {
                        MatchMethod::Jar => format!("{p} [jar]"),
                        MatchMethod::Name => format!("{p} [name]"),
                    })
                    .collect();
                let matched = if matched.is_empty() {
                    "(not used)".to_owned()
                } else {
                    matched.join(", ")
                };
                let _ = writeln!(out, "  {}:{version}  {matched}", n.fqn);
            }
        }
        let unknown: Vec<&str> = self
            .nodes
            .iter()
            .filter(|n| matches!(n.kind, NodeKind::Unknown))
            .map(|n| n.fqn.as_str())
            .collect();
        if !unknown.is_empty() {
            let _ = writeln!(out, "  unknown: {}", unknown.join(", "));
        }

        let _ = writeln!(out, "\ncycles:");
        for c in &self.cycles {
            let names: Vec<&str> = c
                .packages
                .iter()
                .map(|p| self.node(*p).fqn.as_str())
                .collect();
            let (from, to, weight) = c.weakest;
            let _ = writeln!(
                out,
                "  ⟲ {}  — weakest: {} → {} ({weight})",
                names.join(" ⇄ "),
                self.node(from).fqn,
                self.node(to).fqn
            );
        }

        let _ = writeln!(out, "\njpms violations:");
        for v in &self.violations {
            let why = match v.kind {
                ViolationKind::NotExported => "package not exported",
                ViolationKind::NotRead => "module not required",
            };
            let at: Vec<String> = v
                .evidence
                .iter()
                .map(|e| format!("{}:{}", e.file, e.line))
                .collect();
            let _ = writeln!(
                out,
                "  ✗ {} → {}: {why}  ({})",
                self.node(v.from_module).fqn,
                self.node(v.to_package).fqn,
                at.join(", ")
            );
        }

        let _ = writeln!(out, "\nbuild file vs code:");
        for f in &self.findings {
            let what = match f.kind {
                FindingKind::UnusedModuleDependency | FindingKind::UnusedLibrary => {
                    "✕ declared, unused"
                }
                FindingKind::UndeclaredModuleDependency { reachable: true } => {
                    "! used, not declared (arrives transitively)"
                }
                FindingKind::UndeclaredModuleDependency { reachable: false } => {
                    "!! used, not declared (not reachable)"
                }
                FindingKind::UndeclaredLibrary => "! used, not declared",
            };
            let _ = writeln!(
                out,
                "  {what}: {} → {}",
                self.node(f.module).fqn,
                self.node(f.target).fqn
            );
        }

        let _ = writeln!(out, "\nmetrics (main code, JDK excluded):");
        for n in &self.nodes {
            if let Some(m) = self.metrics(n.id) {
                let i = m
                    .instability()
                    .map(|i| format!("{i:.2}"))
                    .unwrap_or_else(|| "–".into());
                let indent = if matches!(n.kind, NodeKind::Module { .. }) {
                    ""
                } else {
                    "  "
                };
                let _ = writeln!(
                    out,
                    "  {indent}{}  Ca {} · Ce {} · I {i}",
                    n.fqn, m.ca, m.ce
                );
            }
        }
        out
    }
}
