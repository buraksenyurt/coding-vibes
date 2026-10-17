//! java-atlas-core — the heart of java-atlas.
//!
//! This crate turns raw *facts* about a Java project (modules, files, imports)
//! into an *atlas*: a resolved dependency graph plus a map layout.
//! It never touches the disk; the scanner crate feeds it.
//!
//! Phase 0 only defines the vocabulary of the map. The real model arrives in
//! phase 1 (facts) and phase 3 (graph).

/// The three zoom levels of the atlas (see PLAN.md, section 3.1).
#[derive(Debug, Clone, Copy, PartialEq, Eq)]
pub enum Scale {
    /// Build modules as regions, external libraries as islands.
    Country,
    /// The packages (cities) of one module.
    Region,
    /// The types (buildings) of one package.
    City,
}

impl Scale {
    /// The next, more detailed scale; `None` when already at the closest one.
    pub fn zoom_in(self) -> Option<Scale> {
        match self {
            Scale::Country => Some(Scale::Region),
            Scale::Region => Some(Scale::City),
            Scale::City => None,
        }
    }

    /// The next, less detailed scale; `None` when already at the widest one.
    pub fn zoom_out(self) -> Option<Scale> {
        match self {
            Scale::Country => None,
            Scale::Region => Some(Scale::Country),
            Scale::City => Some(Scale::Region),
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn zooming_walks_the_three_scales_in_order() {
        assert_eq!(Scale::Country.zoom_in(), Some(Scale::Region));
        assert_eq!(Scale::Region.zoom_in(), Some(Scale::City));
        assert_eq!(Scale::City.zoom_in(), None);
    }

    #[test]
    fn zooming_out_is_the_inverse_of_zooming_in() {
        for scale in [Scale::Country, Scale::Region, Scale::City] {
            if let Some(closer) = scale.zoom_in() {
                assert_eq!(closer.zoom_out(), Some(scale));
            }
        }
    }
}
