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
    this.land();
    const w = Math.min(this.maxW, Math.max(this.minW, this.box.w * factor));
    const f = w / this.box.w;
    this.box = {
      x: p.x - (p.x - this.box.x) * f,
      y: p.y - (p.y - this.box.y) * f,
      w,
      h: this.box.h * f,
    };
  }

  private flight = 0;

  /**
   * Glides to `r`. Position moves linearly, size geometrically: halving the
   * width takes as long at any zoom, which is how flying over a map feels.
   */
  flyTo(r: RectDto, ms = 450, margin = 0.04) {
    const from = { ...this.box };
    this.fit(r, margin);
    const to = { ...this.box };
    const reduced = typeof matchMedia === "function" && matchMedia("(prefers-reduced-motion: reduce)").matches;
    if (reduced || ms <= 0) return;

    const id = ++this.flight;
    const started = performance.now();
    const ease = (t: number) => (t < 0.5 ? 4 * t * t * t : 1 - (-2 * t + 2) ** 3 / 2);
    const step = (now: number) => {
      if (id !== this.flight) return; // a newer flight or a user action took over
      const t = ease(Math.min(1, (now - started) / ms));
      const w = from.w * (to.w / from.w) ** t;
      const h = from.h * (to.h / from.h) ** t;
      const cx = from.x + from.w / 2 + (to.x + to.w / 2 - from.x - from.w / 2) * t;
      const cy = from.y + from.h / 2 + (to.y + to.h / 2 - from.y - from.h / 2) * t;
      this.box = { x: cx - w / 2, y: cy - h / 2, w, h };
      if (t < 1) requestAnimationFrame(step);
    };
    this.box = from;
    requestAnimationFrame(step);
  }

  /** Stops a flight in progress (the user grabbed the map). */
  land() {
    this.flight++;
  }

  /** Moves the camera by a distance in world units. */
  panBy(dx: number, dy: number) {
    this.land();
    this.box = { ...this.box, x: this.box.x - dx, y: this.box.y - dy };
  }

  get viewBox(): string {
    const b = this.box;
    return `${b.x} ${b.y} ${b.w} ${b.h}`;
  }
}
