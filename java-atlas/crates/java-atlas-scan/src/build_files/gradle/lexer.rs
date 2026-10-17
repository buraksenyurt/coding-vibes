//! A tiny tokenizer for Gradle build scripts (Groovy *and* Kotlin DSL).
//!
//! We do not need a real Groovy/Kotlin parser. To find
//! `implementation("g:a:v")` or `api project(':core')` it is enough to know
//! where identifiers, strings and punctuation are — and, crucially, to skip
//! comments and string contents, so that `// implementation("x:y:1")` or a
//! brace inside a string never confuses us.

/// One meaningful piece of a script.
#[derive(Debug, Clone, PartialEq, Eq)]
pub(crate) enum Tok {
    /// `implementation`, `libs`, `java-library` (from backticks)…
    Ident(String),
    /// The *content* of a string literal, quotes removed, escapes kept raw.
    Str(String),
    /// Any other single character: `(`, `)`, `{`, `}`, `.`, `:`, `,`, `=`…
    Sym(char),
    /// A number or other literal we never look at.
    Other,
}

#[derive(Debug, Clone, PartialEq, Eq)]
pub(crate) struct Token {
    pub tok: Tok,
    /// 1-based line, for warnings.
    pub line: u32,
}

pub(crate) fn lex(src: &str) -> Vec<Token> {
    let chars: Vec<char> = src.chars().collect();
    let mut tokens = Vec::new();
    let mut i = 0;
    let mut line = 1u32;

    // Small helper: does `chars` continue with `pat` at position `at`?
    let starts = |at: usize, pat: &str| {
        pat.chars()
            .enumerate()
            .all(|(k, c)| chars.get(at + k) == Some(&c))
    };

    while i < chars.len() {
        let c = chars[i];
        match c {
            '\n' => {
                line += 1;
                i += 1;
            }
            c if c.is_whitespace() => i += 1,

            // Comments
            '/' if starts(i, "//") => {
                while i < chars.len() && chars[i] != '\n' {
                    i += 1;
                }
            }
            '/' if starts(i, "/*") => {
                i += 2;
                while i < chars.len() && !starts(i, "*/") {
                    if chars[i] == '\n' {
                        line += 1;
                    }
                    i += 1;
                }
                i += 2;
            }

            // Strings: """…""", '''…''', "…", '…'
            '"' | '\'' => {
                let start_line = line;
                let triple: String = std::iter::repeat_n(c, 3).collect();
                let mut content = String::new();
                if starts(i, &triple) {
                    i += 3;
                    while i < chars.len() && !starts(i, &triple) {
                        if chars[i] == '\n' {
                            line += 1;
                        }
                        content.push(chars[i]);
                        i += 1;
                    }
                    i += 3;
                } else {
                    i += 1;
                    while i < chars.len() && chars[i] != c && chars[i] != '\n' {
                        if chars[i] == '\\' && i + 1 < chars.len() {
                            content.push(chars[i]);
                            i += 1;
                        }
                        content.push(chars[i]);
                        i += 1;
                    }
                    i += 1; // closing quote
                }
                tokens.push(Token {
                    tok: Tok::Str(content),
                    line: start_line,
                });
            }

            // Kotlin backtick identifiers: `java-library`
            '`' => {
                let end = chars[i + 1..]
                    .iter()
                    .position(|&ch| ch == '`' || ch == '\n');
                let end = end.map_or(chars.len(), |e| i + 1 + e);
                let name: String = chars[i + 1..end].iter().collect();
                tokens.push(Token {
                    tok: Tok::Ident(name),
                    line,
                });
                i = end + 1;
            }

            c if c.is_alphabetic() || c == '_' || c == '$' => {
                let start = i;
                while i < chars.len()
                    && (chars[i].is_alphanumeric() || chars[i] == '_' || chars[i] == '$')
                {
                    i += 1;
                }
                tokens.push(Token {
                    tok: Tok::Ident(chars[start..i].iter().collect()),
                    line,
                });
            }

            c if c.is_ascii_digit() => {
                while i < chars.len()
                    && (chars[i].is_alphanumeric() || chars[i] == '.' || chars[i] == '_')
                {
                    i += 1;
                }
                tokens.push(Token {
                    tok: Tok::Other,
                    line,
                });
            }

            c => {
                tokens.push(Token {
                    tok: Tok::Sym(c),
                    line,
                });
                i += 1;
            }
        }
    }
    tokens
}

#[cfg(test)]
mod tests {
    use super::*;

    fn toks(src: &str) -> Vec<Tok> {
        lex(src).into_iter().map(|t| t.tok).collect()
    }

    fn ident(s: &str) -> Tok {
        Tok::Ident(s.into())
    }

    #[test]
    fn kotlin_call() {
        assert_eq!(
            toks(r#"implementation("g:a:1")"#),
            vec![
                ident("implementation"),
                Tok::Sym('('),
                Tok::Str("g:a:1".into()),
                Tok::Sym(')')
            ]
        );
    }

    #[test]
    fn groovy_call_without_parentheses() {
        assert_eq!(
            toks("api project(':core')")[..3],
            [ident("api"), ident("project"), Tok::Sym('(')]
        );
    }

    #[test]
    fn comments_disappear() {
        let src = "// implementation(\"x:y:1\")\n/* api(\"a:b:2\") */ runtimeOnly";
        assert_eq!(toks(src), vec![ident("runtimeOnly")]);
    }

    #[test]
    fn braces_inside_strings_are_not_symbols() {
        assert_eq!(
            toks(r#"x = "{ not a block }""#),
            vec![
                ident("x"),
                Tok::Sym('='),
                Tok::Str("{ not a block }".into())
            ]
        );
    }

    #[test]
    fn backticks_make_one_identifier() {
        assert_eq!(toks("`java-library`"), vec![ident("java-library")]);
    }

    #[test]
    fn lines_are_counted_across_block_comments_and_strings() {
        let tokens = lex("a\n/*\n\n*/\n\"\"\"x\ny\"\"\"\nb");
        assert_eq!(tokens.last().unwrap().line, 7);
    }
}
