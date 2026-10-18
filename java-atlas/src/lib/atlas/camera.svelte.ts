// The camera over the map: an SVG viewBox in world units.
// Zooming changes its size, panning its position; the drawing never moves.
import type { RectDto } from "../bindings/RectDto";

export interface Box {
  x: number;
  y: number;
  w: number;
  h: number;
}

/**
 * The map element in pixels, and how much of its right side is covered
 * (by the details drawer): fitting keeps the target in the visible part.
 */
export interface Viewport {
  width: number;
  height: number;
  covered: number;
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

  /**
   * Shows `r` with a margin. Without a viewport the SVG's "meet" keeps the
   * aspect ratio; with one, the box gets the element's own aspect ratio and
   * `r` is centred in the part the drawer leaves visible.
   */
  fit(r: RectDto, margin = 0.04, view?: Viewport) {
    const w = r.w * (1 + 2 * margin);
    const h = r.h * (1 + 2 * margin);
    const visible = view ? view.width - view.covered : 0;
    if (!view || visible <= 0 || view.height <= 0) {
      this.box = { x: r.x + r.w / 2 - w / 2, y: r.y + r.h / 2 - h / 2, w, h };
      return;
    }
    const perPixel = Math.max(w / visible, h / view.height);
    const cx = r.x + r.w / 2;
    const cy = r.y + r.h / 2;
    this.box = { x: cx - (perPixel * visible) / 2, y: cy - (perPixel * view.height) / 2, w: perPixel * view.width, h: perPixel * view.height };
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
  flyTo(r: RectDto, ms = 450, margin = 0.04, view?: Viewport) {
    const from = { ...this.box };
    this.fit(r, margin, view);
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
