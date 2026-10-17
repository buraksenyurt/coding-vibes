//! String interning: store each distinct string once, refer to it by a `u32`.
//!
//! A Java project repeats the same names thousands of times: every file in
//! `com.atlasbank.service.account` carries that package name, every import of
//! `java.util.List` carries that type name. The symbol table would hash and
//! compare those long strings over and over. Interning turns them into small
//! integers: hashing a `Sym` is hashing a `u32`, comparing two is one
//! instruction, and a `Sym` is `Copy` — no lifetimes, no clones.

use std::collections::HashMap;

/// A handle to an interned string. Only meaningful with the [`Interner`] that made it.
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, PartialOrd, Ord)]
pub struct Sym(u32);

#[derive(Debug, Default)]
pub struct Interner {
    ids: HashMap<Box<str>, Sym>,
    strings: Vec<Box<str>>,
}

impl Interner {
    /// Returns the handle for `s`, storing it on first sight.
    pub fn intern(&mut self, s: &str) -> Sym {
        if let Some(&sym) = self.ids.get(s) {
            return sym;
        }
        let sym = Sym(self.strings.len() as u32);
        self.strings.push(s.into());
        self.ids.insert(s.into(), sym);
        sym
    }

    /// Looks `s` up without storing it. `None` means "never seen", which is
    /// itself an answer: no type with that name exists in the project.
    pub fn get(&self, s: &str) -> Option<Sym> {
        self.ids.get(s).copied()
    }

    pub fn resolve(&self, sym: Sym) -> &str {
        &self.strings[sym.0 as usize]
    }

    pub fn len(&self) -> usize {
        self.strings.len()
    }

    pub fn is_empty(&self) -> bool {
        self.strings.is_empty()
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn the_same_string_gets_the_same_symbol() {
        let mut i = Interner::default();
        let a = i.intern("com.atlasbank.common");
        let b = i.intern("com.atlasbank.domain");
        assert_eq!(i.intern("com.atlasbank.common"), a);
        assert_ne!(a, b);
        assert_eq!(i.len(), 2);
        assert_eq!(i.resolve(b), "com.atlasbank.domain");
    }

    #[test]
    fn get_does_not_store() {
        let mut i = Interner::default();
        assert_eq!(i.get("x"), None);
        let x = i.intern("x");
        assert_eq!(i.get("x"), Some(x));
        assert_eq!(i.len(), 1);
    }
}
