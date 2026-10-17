//! Where everything sits on the map.
//!
//! One layout for the whole atlas, in one coordinate system ("world units"):
//! regions (modules) tile the land with a squarified treemap, each region is
//! tiled by its cities (packages), each city by its buildings (types).
//! Zooming in is then just moving the camera; the city you saw from far away
//! is exactly where you find it up close. Islands (libraries) sit in the sea
//! east of the land, grouped into archipelagos by groupId; the mainland (JDK)
//! is a strip of coast to the west.
//!
//! The layout depends only on the atlas, never on filters: toggling test code
//! must not make the country move.

use std::collections::BTreeMap;

use super::{Atlas, NodeId, NodeKind};
use crate::geometry::{Rect, squarify};

/// The land, in world units. 1000 × 680 is roughly the window's shape.
pub const LAND: Rect = Rect::new(0.0, 0.0, 1000.0, 680.0);

/// Smallest share of its parent a child gets, so tiny modules and packages
/// stay visible and clickable.
const MIN_SHARE: f64 = 0.03;

const SEA_GAP: f64 = 70.0;
/// Widest an archipelago grows before wrapping its islands.
const COLUMN_WIDTH: f64 = 300.0;
/// Width of the sea strip holding the archipelagos.
const SEA_WIDTH: f64 = 640.0;
const ISLAND_GAP: f64 = 26.0;
const ARCHIPELAGO_GAP: f64 = 26.0;
/// Space under an island for its name.
const ISLAND_LABEL: f64 = 22.0;
const ARCHIPELAGO_HEADER: f64 = 28.0;
const MAINLAND_WIDTH: f64 = 170.0;
const PORT_HEIGHT: f64 = 24.0;

/// Islands of one groupId (or the unknown packages), framed together.
#[derive(Debug, Clone, PartialEq)]
pub struct Archipelago {
    /// The groupId, or `"?"` for packages no library explains.
    pub name: String,
    pub frame: Rect,
    pub islands: Vec<NodeId>,
}

#[derive(Debug, Clone, PartialEq)]
pub struct Layout {
    /// Everything, with a margin: the camera's "fit" rectangle.
    pub world: Rect,
    pub land: Rect,
    /// The JDK coast; empty when the code uses no JDK package.
    pub mainland: Rect,
    /// Indexed by node: regions, cities, buildings, islands and ports.
    pub rects: Vec<Option<Rect>>,
    pub archipelagos: Vec<Archipelago>,
}

impl Layout {
    pub fn rect(&self, id: NodeId) -> Option<Rect> {
        self.rects[id.index()]
    }
}

/// Room for a label at the top of a tile, proportional to its size.
fn header(r: &Rect) -> f64 {
    (r.w.min(r.h) * 0.10).clamp(4.0, 28.0)
}

fn padding(r: &Rect) -> f64 {
    (r.w.min(r.h) * 0.03).clamp(1.0, 8.0)
}

impl Atlas {
    pub fn layout(&self) -> Layout {
        let sizes = self.sizes();
        let mut rects: Vec<Option<Rect>> = vec![None; self.nodes.len()];
        // parent → children, built once (a scan per tile would be quadratic).
        let mut children: Vec<Vec<NodeId>> = vec![Vec::new(); self.nodes.len()];
        let mut modules = Vec::new();
        for n in &self.nodes {
            match (n.parent, &n.kind) {
                (Some(p), _) => children[p.index()].push(n.id),
                (None, NodeKind::Module { .. }) => modules.push(n.id),
                _ => {}
            }
        }

        // Regions → cities → buildings, each a treemap inside its parent.
        let mut level = vec![(LAND.inset(8.0), modules)];
        while let Some((area, ids)) = level.pop() {
            if ids.is_empty() {
                continue;
            }
            let total: f64 = ids
                .iter()
                .map(|id| f64::from(sizes[id.index()]))
                .sum::<f64>()
                .max(1.0);
            let values: Vec<f64> = ids
                .iter()
                .map(|id| f64::from(sizes[id.index()]).max(total * MIN_SHARE).max(1.0))
                .collect();
            for (id, tile) in ids.iter().zip(squarify(&values, area)) {
                // A gap between neighbours is the border line on the map.
                let tile = tile.inset(padding(&tile));
                rects[id.index()] = Some(tile);
                let inner = tile.inset(padding(&tile)).below_header(header(&tile));
                level.push((inner, children[id.index()].clone()));
            }
        }

        let archipelagos = self.place_islands(&mut rects);
        let mainland = self.place_mainland(&mut rects);

        let mut world = LAND;
        for a in &archipelagos {
            world = world.union(&a.frame);
        }
        if mainland.area() > 0.0 {
            world = world.union(&mainland);
        }
        Layout {
            world: world.inset(-40.0),
            land: LAND,
            mainland,
            rects,
            archipelagos,
        }
    }

    /// How much code uses each external node (all modules, main + test).
    fn usage(&self) -> Vec<u32> {
        let mut usage = vec![0u32; self.nodes.len()];
        for e in &self.module_edges {
            usage[e.to.index()] += e.weight.total();
        }
        usage
    }

    fn place_islands(&self, rects: &mut [Option<Rect>]) -> Vec<Archipelago> {
        let usage = self.usage();
        // groupId → islands. BTreeMap: deterministic before the usage sort.
        let mut groups: BTreeMap<String, Vec<NodeId>> = BTreeMap::new();
        for n in &self.nodes {
            let group = match &n.kind {
                NodeKind::Library { group, .. } => group.clone(),
                NodeKind::Unknown => "?".to_owned(),
                _ => continue,
            };
            groups.entry(group).or_default().push(n.id);
        }
        let weight = |ids: &[NodeId]| ids.iter().map(|id| usage[id.index()]).sum::<u32>();
        let mut groups: Vec<(String, Vec<NodeId>)> = groups.into_iter().collect();
        // Busiest archipelagos nearest the land's top; the unknown one last.
        groups.sort_by(|a, b| {
            (a.0 == "?")
                .cmp(&(b.0 == "?"))
                .then(weight(&b.1).cmp(&weight(&a.1)))
        });

        let radius = |id: NodeId| (28.0 + 6.0 * f64::from(usage[id.index()] + 1).log2()).min(64.0);

        // Shelf packing: archipelagos left to right across the sea, a new
        // shelf below when the row is full. Keeps the sea about as tall as
        // the land instead of growing a long thin column.
        let mut out = Vec::new();
        let left = LAND.right() + SEA_GAP;
        let (mut x, mut y, mut shelf_h) = (left, LAND.y, 0.0f64);
        for (name, mut ids) in groups {
            ids.sort_by(|a, b| {
                usage[b.index()]
                    .cmp(&usage[a.index()])
                    .then(self.node(*a).name.cmp(&self.node(*b).name))
            });

            // Islands in rows inside the archipelago, relative to its corner.
            let mut placed = Vec::new();
            let (mut ix, mut row_y, mut row_h) = (0.0f64, ARCHIPELAGO_HEADER, 0.0f64);
            for id in &ids {
                let d = 2.0 * radius(*id);
                if ix > 0.0 && ix + d > COLUMN_WIDTH {
                    ix = 0.0;
                    row_y += row_h + ISLAND_LABEL + ISLAND_GAP;
                    row_h = 0.0;
                }
                placed.push((*id, Rect::new(ix, row_y, d, d)));
                ix += d + ISLAND_GAP;
                row_h = row_h.max(d);
            }
            // Room under the last row for the island names; room for the group name.
            let height = row_y + row_h + ISLAND_LABEL;
            let width = placed
                .iter()
                .map(|(_, r)| r.right())
                .fold(0.0, f64::max)
                .max(COLUMN_WIDTH * 0.45);

            if x > left && x + width > left + SEA_WIDTH {
                x = left;
                y += shelf_h + ARCHIPELAGO_GAP;
                shelf_h = 0.0;
            }
            for (id, r) in &placed {
                rects[id.index()] = Some(Rect::new(x + r.x, y + r.y, r.w, r.h));
            }
            out.push(Archipelago {
                name,
                frame: Rect::new(x, y, width, height).inset(-8.0),
                islands: ids,
            });
            x += width + ARCHIPELAGO_GAP;
            shelf_h = shelf_h.max(height);
        }
        out
    }

    /// JDK packages as ports along a coast west of the land.
    fn place_mainland(&self, rects: &mut [Option<Rect>]) -> Rect {
        let mut ports: Vec<NodeId> = self
            .nodes
            .iter()
            .filter(|n| n.kind == NodeKind::Jdk)
            .map(|n| n.id)
            .collect();
        if ports.is_empty() {
            return Rect::new(LAND.x - SEA_GAP, LAND.y, 0.0, 0.0);
        }
        ports.sort_by(|a, b| self.node(*a).fqn.cmp(&self.node(*b).fqn));
        let x = LAND.x - SEA_GAP - MAINLAND_WIDTH;
        for (i, id) in ports.iter().enumerate() {
            rects[id.index()] = Some(Rect::new(
                x + 10.0,
                LAND.y + 30.0 + i as f64 * PORT_HEIGHT,
                MAINLAND_WIDTH - 20.0,
                PORT_HEIGHT - 4.0,
            ));
        }
        let height = (30.0 + ports.len() as f64 * PORT_HEIGHT + 10.0).max(LAND.h);
        Rect::new(x, LAND.y, MAINLAND_WIDTH, height)
    }
}
