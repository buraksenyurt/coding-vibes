//! One `.java` file → [`SourceFileFacts`]; one `module-info.java` → [`JpmsFacts`].
//!
//! This is where we meet the concrete syntax tree for real. Two styles are
//! used on purpose:
//!
//! - **Structured walking** for declarations (package, imports, types): we
//!   know exactly where they live, so we go straight there via field names.
//! - **A full iterative traversal** with a `TreeCursor` for references: type
//!   names can appear almost anywhere (fields, generics, casts, lambdas…).

use std::collections::{BTreeMap, HashSet};

use java_atlas_core::facts::{
    ImportFacts, JpmsFacts, NameRef, PackageGrant, Provides, Requires, SourceFileFacts, TypeFacts,
    TypeKind,
};
use tree_sitter::{Node, Parser, Tree};

/// The text a node covers. Never fails for a tree parsed from `src`.
fn text<'s>(node: Node, src: &'s str) -> &'s str {
    &src[node.byte_range()]
}

/// 1-based line of a node.
fn line(node: Node) -> u32 {
    node.start_position().row as u32 + 1
}

/// Qualified names may be written with spaces or comments between the dots
/// (`java . util . List` is legal Java); we keep only the name characters.
fn compact(s: &str) -> String {
    s.chars().filter(|c| !c.is_whitespace()).collect()
}

fn type_kind(node_kind: &str) -> Option<TypeKind> {
    Some(match node_kind {
        "class_declaration" => TypeKind::Class,
        "interface_declaration" => TypeKind::Interface,
        "enum_declaration" => TypeKind::Enum,
        "record_declaration" => TypeKind::Record,
        "annotation_type_declaration" => TypeKind::Annotation,
        _ => return None,
    })
}

pub(crate) fn parse(parser: &mut Parser, src: &str) -> Option<Tree> {
    parser.parse(src, None)
}

// ---------------------------------------------------------------------------
// Ordinary source files
// ---------------------------------------------------------------------------

pub(crate) fn file_facts(tree: &Tree, src: &str, path: String, is_test: bool) -> SourceFileFacts {
    let root = tree.root_node();
    let mut facts = SourceFileFacts {
        path,
        package: String::new(),
        is_test,
        lines: src.lines().count() as u32,
        imports: Vec::new(),
        types: Vec::new(),
        references: Vec::new(),
        has_errors: root.has_error(),
    };
    // Generic parameters (`T`, `R`…) look exactly like type names; we collect
    // them while reading declarations and filter them out of the references.
    let mut type_params = HashSet::new();

    let mut cursor = root.walk();
    for node in root.named_children(&mut cursor) {
        match node.kind() {
            "package_declaration" => {
                if let Some(name) = qualified_name_child(node) {
                    facts.package = compact(text(name, src));
                }
            }
            "import_declaration" => facts.imports.push(import(node, src)),
            kind if type_kind(kind).is_some() => {
                collect_types(node, src, "", &mut facts.types, &mut type_params);
            }
            _ => {}
        }
    }

    facts.references = references(root, src, &type_params);
    facts
}

/// The `identifier` or `scoped_identifier` child of a package/import declaration.
fn qualified_name_child(node: Node) -> Option<Node> {
    let mut cursor = node.walk();
    node.named_children(&mut cursor)
        .find(|n| matches!(n.kind(), "identifier" | "scoped_identifier"))
}

fn import(node: Node, src: &str) -> ImportFacts {
    let mut cursor = node.walk();
    // `static` is an anonymous token child; `*` is the named node `asterisk`.
    let mut is_static = false;
    let mut on_demand = false;
    for child in node.children(&mut cursor) {
        match child.kind() {
            "static" => is_static = true,
            "asterisk" => on_demand = true,
            _ => {}
        }
    }
    ImportFacts {
        path: qualified_name_child(node)
            .map(|n| compact(text(n, src)))
            .unwrap_or_default(),
        is_static,
        on_demand,
        line: line(node),
    }
}

fn is_public(decl: Node) -> bool {
    let mut cursor = decl.walk();
    decl.children(&mut cursor)
        .filter(|c| c.kind() == "modifiers")
        .any(|m| {
            let mut inner = m.walk();
            m.children(&mut inner).any(|t| t.kind() == "public")
        })
}

/// Records a type declaration and, recursively, its member types.
fn collect_types(
    decl: Node,
    src: &str,
    outer: &str,
    out: &mut Vec<TypeFacts>,
    params: &mut HashSet<String>,
) {
    let Some(kind) = type_kind(decl.kind()) else {
        return;
    };
    let Some(name_node) = decl.child_by_field_name("name") else {
        return;
    };
    let simple = text(name_node, src);
    let name = if outer.is_empty() {
        simple.to_owned()
    } else {
        format!("{outer}.{simple}")
    };

    if let Some(tp) = decl.child_by_field_name("type_parameters") {
        type_parameter_names(tp, src, params);
    }

    out.push(TypeFacts {
        name: name.clone(),
        kind,
        is_public: is_public(decl),
        line: line(decl),
        lines: (decl.end_position().row - decl.start_position().row) as u32 + 1,
    });

    let Some(body) = decl.child_by_field_name("body") else {
        return;
    };
    let mut cursor = body.walk();
    for member in body.named_children(&mut cursor) {
        match member.kind() {
            // enum E { A, B; class Nested {} } — members sit one level deeper.
            "enum_body_declarations" => {
                let mut inner = member.walk();
                for m in member.named_children(&mut inner) {
                    collect_types(m, src, &name, out, params);
                    method_type_params(m, src, params);
                }
            }
            _ => {
                collect_types(member, src, &name, out, params);
                method_type_params(member, src, params);
            }
        }
    }
}

fn method_type_params(member: Node, src: &str, params: &mut HashSet<String>) {
    if matches!(
        member.kind(),
        "method_declaration" | "constructor_declaration"
    ) && let Some(tp) = member.child_by_field_name("type_parameters")
    {
        type_parameter_names(tp, src, params);
    }
}

fn type_parameter_names(type_parameters: Node, src: &str, out: &mut HashSet<String>) {
    let mut cursor = type_parameters.walk();
    for tp in type_parameters.named_children(&mut cursor) {
        // type_parameter → (annotations)* type_identifier type_bound?
        let mut inner = tp.walk();
        if let Some(id) = tp
            .named_children(&mut inner)
            .find(|n| n.kind() == "type_identifier")
        {
            out.insert(text(id, src).to_owned());
        }
    }
}

/// Names that may denote types, anywhere in the file except package/imports.
fn references(root: Node, src: &str, type_params: &HashSet<String>) -> Vec<NameRef> {
    let mut found: BTreeMap<String, (u32, u32)> = BTreeMap::new();
    let mut add = |name: String, at: u32| {
        let entry = found.entry(name).or_insert((at, 0));
        entry.1 += 1;
    };
    // `Money.zero()`, `BigDecimal.ZERO`, `AccountSummary::of`: the left side is
    // an `identifier`, not a `type_identifier`. Java naming conventions make a
    // capital first letter a strong hint that it is a type.
    let looks_like_type =
        |n: Node| n.kind() == "identifier" && text(n, src).starts_with(char::is_uppercase);

    // Iterative pre-order traversal: no recursion, so no stack overflow on
    // deeply nested expressions (generated code can be very deep).
    let mut cursor = root.walk();
    'walk: loop {
        let node = cursor.node();
        let mut descend = true;

        match node.kind() {
            "package_declaration" | "import_declaration" => descend = false,
            "scoped_type_identifier" => {
                // `java.util.concurrent.atomic.AtomicLong`: take the whole thing once.
                add(compact(text(node, src)), line(node));
                descend = false;
            }
            "type_identifier" => {
                let name = text(node, src);
                if name != "var" && !type_params.contains(name) {
                    add(name.to_owned(), line(node));
                }
            }
            "method_invocation" | "field_access" => {
                if let Some(object) = node
                    .child_by_field_name("object")
                    .filter(|o| looks_like_type(*o))
                {
                    add(text(object, src).to_owned(), line(object));
                }
            }
            "method_reference" => {
                if let Some(first) = node.named_child(0).filter(|o| looks_like_type(*o)) {
                    add(text(first, src).to_owned(), line(first));
                }
            }
            "marker_annotation" | "annotation" => {
                if let Some(name) = node.child_by_field_name("name") {
                    add(compact(text(name, src)), line(name));
                }
            }
            _ => {}
        }

        if descend && cursor.goto_first_child() {
            continue;
        }
        // Climb until a next sibling exists; reaching the root again means done.
        loop {
            if cursor.goto_next_sibling() {
                continue 'walk;
            }
            if !cursor.goto_parent() {
                break 'walk;
            }
        }
    }

    found
        .into_iter()
        .map(|(name, (line, count))| NameRef { name, line, count })
        .collect()
}

// ---------------------------------------------------------------------------
// module-info.java
// ---------------------------------------------------------------------------

pub(crate) fn module_info(tree: &Tree, src: &str, file: String) -> Option<JpmsFacts> {
    let root = tree.root_node();
    let mut cursor = root.walk();
    let decl = root
        .named_children(&mut cursor)
        .find(|n| n.kind() == "module_declaration")?;

    let mut facts = JpmsFacts {
        name: compact(text(decl.child_by_field_name("name")?, src)),
        open: {
            let mut c = decl.walk();
            decl.children(&mut c).any(|t| t.kind() == "open")
        },
        file,
        requires: Vec::new(),
        exports: Vec::new(),
        opens: Vec::new(),
        uses: Vec::new(),
        provides: Vec::new(),
    };

    let body = decl.child_by_field_name("body")?;
    let mut cursor = body.walk();
    for directive in body.named_children(&mut cursor) {
        let field = |name: &str| {
            directive
                .child_by_field_name(name)
                .map(|n| compact(text(n, src)))
        };
        let fields = |name: &str| -> Vec<String> {
            let mut c = directive.walk();
            directive
                .children_by_field_name(name, &mut c)
                .map(|n| compact(text(n, src)))
                .collect()
        };
        match directive.kind() {
            "requires_module_directive" => {
                let modifiers = fields("modifiers");
                if let Some(module) = field("module") {
                    facts.requires.push(Requires {
                        module,
                        transitive: modifiers.iter().any(|m| m == "transitive"),
                        is_static: modifiers.iter().any(|m| m == "static"),
                    });
                }
            }
            "exports_module_directive" | "opens_module_directive" => {
                if let Some(package) = field("package") {
                    let grant = PackageGrant {
                        package,
                        to: fields("modules"),
                    };
                    if directive.kind() == "exports_module_directive" {
                        facts.exports.push(grant);
                    } else {
                        facts.opens.push(grant);
                    }
                }
            }
            "uses_module_directive" => facts.uses.extend(field("type")),
            "provides_module_directive" => {
                // provides <provided> with <impl>, <impl>…  — the implementations
                // have no field name, so take every named child after the first.
                let mut c = directive.walk();
                let names: Vec<String> = directive
                    .named_children(&mut c)
                    .filter(|n| matches!(n.kind(), "identifier" | "scoped_identifier"))
                    .map(|n| compact(text(n, src)))
                    .collect();
                if let Some((service, with)) = names.split_first() {
                    facts.provides.push(Provides {
                        service: service.clone(),
                        with: with.to_vec(),
                    });
                }
            }
            _ => {}
        }
    }
    Some(facts)
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::java_parser;

    fn facts(src: &str) -> SourceFileFacts {
        let tree = parse(&mut java_parser().unwrap(), src).unwrap();
        file_facts(&tree, src, "T.java".into(), false)
    }

    fn names(f: &SourceFileFacts) -> Vec<&str> {
        f.references.iter().map(|r| r.name.as_str()).collect()
    }

    #[test]
    fn imports_in_every_shape() {
        let f = facts(
            "package a.b;\nimport x.Y;\nimport x.y.*;\nimport static x.Y.z;\nimport static x.Y.*;\nclass A {}",
        );
        let shape: Vec<(&str, bool, bool, u32)> = f
            .imports
            .iter()
            .map(|i| (i.path.as_str(), i.is_static, i.on_demand, i.line))
            .collect();
        assert_eq!(
            shape,
            [
                ("x.Y", false, false, 2),
                ("x.y", false, true, 3),
                ("x.Y.z", true, false, 4),
                ("x.Y", true, true, 5)
            ]
        );
        assert_eq!(f.package, "a.b");
    }

    #[test]
    fn member_types_get_dotted_names() {
        let f = facts(
            "public class Outer {\n  enum Status { A; interface Deep {} }\n  record R(int x) {}\n  @interface Tag {}\n}\ninterface Second {}",
        );
        let shape: Vec<(&str, TypeKind, bool)> = f
            .types
            .iter()
            .map(|t| (t.name.as_str(), t.kind, t.is_public))
            .collect();
        assert_eq!(
            shape,
            [
                ("Outer", TypeKind::Class, true),
                ("Outer.Status", TypeKind::Enum, false),
                ("Outer.Status.Deep", TypeKind::Interface, false),
                ("Outer.R", TypeKind::Record, false),
                ("Outer.Tag", TypeKind::Annotation, false),
                ("Second", TypeKind::Interface, false),
            ]
        );
        assert_eq!(f.types[0].lines, 5);
    }

    #[test]
    fn references_come_from_types_calls_and_annotations() {
        let f = facts(
            r#"package p;
            @Deprecated
            class A<T> extends Base implements Api.Port {
                private java.util.concurrent.atomic.AtomicLong n;
                @Override public <R> R map(T in, java.util.function.Function<T, R> f) {
                    var x = Money.zero();
                    Runnable r = Helper::run;
                    Object c = Thing.class;
                    return f.apply(in);
                }
            }"#,
        );
        assert_eq!(
            names(&f),
            [
                "Api.Port",
                "Base",
                "Deprecated",
                "Helper",
                "Money",
                "Object",
                "Override",
                "Runnable",
                "Thing",
                "java.util.concurrent.atomic.AtomicLong",
                "java.util.function.Function",
            ]
        );
    }

    #[test]
    fn generic_parameters_and_var_are_not_types() {
        let f = facts("class A<K> { <V> V get(K k) { var v = (V) null; return v; } }");
        assert!(names(&f).is_empty(), "{:?}", names(&f));
    }

    #[test]
    fn repeated_names_are_counted_once_with_first_line() {
        let f = facts("class A {\n Money a;\n Money b;\n}");
        assert_eq!(
            f.references,
            [NameRef {
                name: "Money".into(),
                line: 2,
                count: 2
            }]
        );
    }

    #[test]
    fn syntax_errors_are_flagged_but_facts_survive() {
        let f = facts("package p;\nimport x.Y;\nclass A { void f( { Y y; }");
        assert!(f.has_errors);
        assert_eq!(f.package, "p");
        assert_eq!(f.imports.len(), 1);
    }

    #[test]
    fn module_info_directives() {
        let src = "open module com.x.app {\n requires transitive com.x.core;\n requires static lombok;\n exports com.x.api;\n exports com.x.spi to com.x.plugin, com.x.test;\n opens com.x.model;\n uses com.x.spi.Hook;\n provides com.x.spi.Hook with com.x.impl.A, com.x.impl.B;\n}";
        let tree = parse(&mut java_parser().unwrap(), src).unwrap();
        let m = module_info(&tree, src, "module-info.java".into()).unwrap();
        assert_eq!(m.name, "com.x.app");
        assert!(m.open);
        assert_eq!(
            m.requires,
            [
                Requires {
                    module: "com.x.core".into(),
                    transitive: true,
                    is_static: false
                },
                Requires {
                    module: "lombok".into(),
                    transitive: false,
                    is_static: true
                },
            ]
        );
        assert_eq!(m.exports[1].to, ["com.x.plugin", "com.x.test"]);
        assert_eq!(m.opens[0].package, "com.x.model");
        assert_eq!(m.uses, ["com.x.spi.Hook"]);
        assert_eq!(m.provides[0].with, ["com.x.impl.A", "com.x.impl.B"]);
    }

    /// The same import extraction written with tree-sitter's *query language*.
    /// Shorter to write, but every capture still needs Rust code around it;
    /// the cursor version above is easier to debug step by step.
    #[test]
    fn a_query_finds_the_same_imports() {
        use streaming_iterator::StreamingIterator;
        use tree_sitter::{Query, QueryCursor};

        let src = "import a.B;\nimport static c.D.e;\nimport f.*;\nclass X {}";
        let tree = parse(&mut java_parser().unwrap(), src).unwrap();
        let query = Query::new(
            &tree_sitter_java::LANGUAGE.into(),
            "(import_declaration [(identifier) (scoped_identifier)] @path)",
        )
        .unwrap();
        let mut qc = QueryCursor::new();
        let mut matches = qc.matches(&query, tree.root_node(), src.as_bytes());
        let mut via_query = Vec::new();
        while let Some(m) = matches.next() {
            via_query.push(text(m.captures()[0].node, src).to_owned());
        }

        let via_cursor: Vec<String> = file_facts(&tree, src, String::new(), false)
            .imports
            .into_iter()
            .map(|i| i.path)
            .collect();
        assert_eq!(via_query, via_cursor);
    }
}
