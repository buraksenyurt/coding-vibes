// Label sizing in world units. Text scales with the map, so a label that
// fits its region at one zoom fits it at every zoom.
import type { RectDto } from "../bindings/RectDto";

/** Rough average glyph width relative to font size for UI sans fonts. */
const GLYPH = 0.58;

export interface Label {
  text: string;
  size: number;
}

/**
 * The largest font (up to `max`) that lets `text` fit `width`; below `min`
 * the text is shortened with an ellipsis instead.
 */
export function fitLabel(text: string, width: number, max: number, min = 3): Label {
  const fitting = width / (text.length * GLYPH);
  if (fitting >= min) return { text, size: Math.min(max, fitting) };
  const chars = Math.max(1, Math.floor(width / (min * GLYPH)) - 1);
  return { text: text.slice(0, chars) + "…", size: min };
}

/** Header height used by the layout in Rust (`header()` in layout.rs). */
export function headerOf(r: RectDto): number {
  return Math.min(28, Math.max(4, Math.min(r.w, r.h) * 0.1));
}

/** The last segment of a package: `com.atlasbank.service.audit` → `audit`. */
export function shortPackage(name: string): string {
  const i = name.lastIndexOf(".");
  return i < 0 ? name : name.slice(i + 1);
}
