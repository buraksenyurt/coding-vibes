// The camera over the map: an SVG viewBox in world units.
// Zooming changes its size, panning its position; the drawing never moves.
import type { RectDto } from "../bindings/RectDto";

export interface Box {
  x: number;
  y: number;
  w: number;
  h: number;
}

export class Camera {
  box = $state<Box>({ x: 0, y: 0, w: 1000, h: 700 });
  /** Zoom limits, relative to the world's width. */
  private minW = 20;
  private maxW = 5000;

  setWorld(world: RectDto) {
    this.minW = world.w * 0.01;
    this.maxW = world.w * 4;
  }

  /** Shows `r` with a margin; the SVG's "meet" keeps the aspect ratio. */
  fit(r: RectDto, margin = 0.04) {
    const mx = r.w * margin;
    const my = r.h * margin;
    this.box = { x: r.x - mx, y: r.y - my, w: r.w + 2 * mx, h: r.h + 2 * my };
  }

  /** Zooms by `factor` (< 1 = closer) keeping the world point `p` under the pointer. */
  zoomAt(p: { x: number; y: number }, factor: number) {
    const w = Math.min(this.maxW, Math.max(this.minW, this.box.w * factor));
    const f = w / this.box.w;
    this.box = {
      x: p.x - (p.x - this.box.x) * f,
      y: p.y - (p.y - this.box.y) * f,
      w,
      h: this.box.h * f,
    };
  }

  /** Moves the camera by a distance in world units. */
  panBy(dx: number, dy: number) {
    this.box = { ...this.box, x: this.box.x - dx, y: this.box.y - dy };
  }

  get viewBox(): string {
    const b = this.box;
    return `${b.x} ${b.y} ${b.w} ${b.h}`;
  }
}
