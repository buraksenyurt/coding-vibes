// Road geometry: where a road leaves a tile, how it bends, where its arrow sits.
import type { RectDto } from "../bindings/RectDto";

export interface Point {
  x: number;
  y: number;
}

export type Shape = "rect" | "ellipse";

export interface Road {
  /** SVG path data. */
  d: string;
  /** Point halfway along the curve, and the direction there (radians). */
  mid: Point;
  angle: number;
}

export function centre(r: RectDto): Point {
  return { x: r.x + r.w / 2, y: r.y + r.h / 2 };
}

/**
 * Where a straight line from the centre of `r` towards `to` crosses its
 * outline. Roads start and end at borders and coasts, not in the middle of
 * a region, so the map does not turn into a hairball at the centres.
 */
export function exitPoint(r: RectDto, shape: Shape, to: Point): Point {
  const c = centre(r);
  const dx = to.x - c.x;
  const dy = to.y - c.y;
  if (dx === 0 && dy === 0) return c;
  const rx = r.w / 2;
  const ry = shape === "ellipse" ? (r.h / 2) * 0.82 : r.h / 2;
  let t: number;
  if (shape === "ellipse") {
    // (t·dx/rx)² + (t·dy/ry)² = 1
    t = 1 / Math.sqrt((dx / rx) ** 2 + (dy / ry) ** 2);
  } else {
    // The first side the ray hits.
    const tx = dx === 0 ? Infinity : rx / Math.abs(dx);
    const ty = dy === 0 ? Infinity : ry / Math.abs(dy);
    t = Math.min(tx, ty);
  }
  return { x: c.x + dx * t, y: c.y + dy * t };
}

/**
 * A gently bent road from `a` to `b`. The bend is always to the right of the
 * direction of travel, so A → B and B → A become two separate lanes instead
 * of one line drawn twice — a cycle between two cities is visible as a loop.
 */
export function road(a: RectDto, aShape: Shape, b: RectDto, bShape: Shape, bend = 0.18): Road {
  const ca = centre(a);
  const cb = centre(b);
  const s = exitPoint(a, aShape, cb);
  const e = exitPoint(b, bShape, ca);
  const dx = e.x - s.x;
  const dy = e.y - s.y;
  const length = Math.hypot(dx, dy) || 1;
  // Normal pointing to the right of s → e (y grows downwards in SVG).
  const nx = -dy / length;
  const ny = dx / length;
  const c = { x: (s.x + e.x) / 2 + nx * length * bend, y: (s.y + e.y) / 2 + ny * length * bend };
  // A quadratic Bézier at t = 0.5: ¼·s + ½·c + ¼·e; its tangent there is e − s.
  const mid = { x: 0.25 * s.x + 0.5 * c.x + 0.25 * e.x, y: 0.25 * s.y + 0.5 * c.y + 0.25 * e.y };
  return {
    d: `M ${s.x} ${s.y} Q ${c.x} ${c.y} ${e.x} ${e.y}`,
    mid,
    angle: Math.atan2(dy, dx),
  };
}

/** An arrow head at `p` pointing along `angle`, `size` world units long. */
export function arrowHead(p: Point, angle: number, size: number): string {
  const back = { x: p.x - Math.cos(angle) * size, y: p.y - Math.sin(angle) * size };
  const wing = size * 0.55;
  const left = { x: back.x + Math.cos(angle + Math.PI / 2) * wing, y: back.y + Math.sin(angle + Math.PI / 2) * wing };
  const right = { x: back.x + Math.cos(angle - Math.PI / 2) * wing, y: back.y + Math.sin(angle - Math.PI / 2) * wing };
  const tip = { x: p.x + Math.cos(angle) * size * 0.4, y: p.y + Math.sin(angle) * size * 0.4 };
  return `M ${tip.x} ${tip.y} L ${left.x} ${left.y} L ${right.x} ${right.y} Z`;
}

/** Stroke width in screen pixels: logarithmic, so one heavy road does not drown the rest. */
export function roadWidth(uses: number): number {
  return Math.min(8, 1 + 1.6 * Math.log2(1 + uses));
}
