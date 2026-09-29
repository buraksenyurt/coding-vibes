// Pure layout maths: column/row indices from Rust become pixels here.
// Keeping it free of Svelte makes it trivial to test and reuse.

import type { MetroMapDto } from "../bindings/MetroMapDto";
import type { TransitionDto } from "../bindings/TransitionDto";
import type { LaneDto } from "../bindings/LaneDto";
import type { ColorMode } from "../view.svelte";

export const COLUMN_WIDTH = 28;
export const ROW_HEIGHT = 40;
/** Space above the first lane for the date ruler. */
export const HEADER_HEIGHT = 32;
export const PADDING_X = 24;
export const LABEL_WIDTH = 200;

export const STATION_RADIUS = 4.5;
export const MERGE_RADIUS = 6.5;

export const x = (column: number): number => PADDING_X + column * COLUMN_WIDTH;
export const y = (row: number): number => HEADER_HEIGHT + row * ROW_HEIGHT + ROW_HEIGHT / 2;

export function canvasSize(map: MetroMapDto, rowOf: (lane: LaneDto) => number): { width: number; height: number } {
  const rows = map.lanes.reduce((max, lane) => Math.max(max, rowOf(lane) + 1), 0);
  return {
    width: x(Math.max(map.commits.length - 1, 0)) + PADDING_X * 4,
    height: HEADER_HEIGHT + rows * ROW_HEIGHT + 8,
  };
}

/** Palette slot for a lane colour index (the core cycles 0..7). */
export const laneColor = (color: number): string => `var(--lane-${color % 8})`;

export interface DateTick {
  column: number;
  label: string;
}

/** A tick wherever the calendar day changes, skipping ticks that would overlap. */
export function dateTicks(map: MetroMapDto, minSpacing = 88): DateTick[] {
  const fmt = new Intl.DateTimeFormat("tr-TR", { day: "numeric", month: "short" });
  const ticks: DateTick[] = [];
  let lastDay = "";
  let lastX = -Infinity;
  map.commits.forEach((commit, column) => {
    const day = new Date(commit.time * 1000).toDateString();
    if (day === lastDay) return;
    lastDay = day;
    if (x(column) - lastX < minSpacing) return;
    lastX = x(column);
    ticks.push({ column, label: fmt.format(new Date(commit.time * 1000)) });
  });
  return ticks;
}

// ---------------------------------------------------------------------------
// Transitions


/**
 * Horizontal extent of a lane's track, in columns. Longer than the lane's own
 * commits: it starts right after the fork (where the fork bend lands) and
 * runs until the column before the merge it flows into. That way the long
 * horizontal runs wear the lane's colour and transitions are only the bends.
 */
export function trackSpan(lane: LaneDto, transitions: TransitionDto[]): { from: number; to: number } {
  const from = lane.forkColumn === null ? lane.firstColumn : Math.min(lane.firstColumn, lane.forkColumn + 1);
  const to = transitions
    .filter((t) => t.fromLane === lane.id && t.kind === "merge")
    .reduce((end, t) => Math.max(end, t.toColumn - 1), lane.lastColumn);
  return { from, to };
}

/**
 * Metro-style connector between two stations.
 *
 * - Fork: leave the parent station and bend onto the new row within one
 *   column; the lane's own track continues from there.
 * - Merge: bend from the source row onto the merge commit within the last
 *   column; the source track has already run up to that column.
 * - Cherry-pick: a single gentle S-curve; it is a copy, not a track.
 */
export function transitionPath(t: TransitionDto, fromRow: number, toRow: number): string {
  const x1 = x(t.fromColumn);
  const y1 = y(fromRow);
  const x2 = x(t.toColumn);
  const y2 = y(toRow);
  const bend = Math.min(COLUMN_WIDTH, x2 - x1);
  const k = bend * 0.55; // control distance for a smooth quarter bend

  switch (t.kind) {
    case "fork":
      return `M ${x1} ${y1} C ${x1 + k} ${y1}, ${x1 + bend - k} ${y2}, ${x1 + bend} ${y2}`;
    case "merge":
      return `M ${x2 - bend} ${y1} C ${x2 - bend + k} ${y1}, ${x2 - k} ${y2}, ${x2} ${y2}`;
    case "cherryPick": {
      const mid = (x2 - x1) / 2;
      return `M ${x1} ${y1} C ${x1 + mid} ${y1}, ${x2 - mid} ${y2}, ${x2} ${y2}`;
    }
  }
}

/** Colour of a transition under the chosen mode. */
export function transitionColor(t: TransitionDto, lanes: LaneDto[], mode: ColorMode): string {
  if (mode === "kind") return `var(--kind-${t.kind})`;
  // By lane: a fork wears the new branch's colour, a merge or copy the source's.
  const lane = t.kind === "fork" ? lanes[t.toLane] : lanes[t.fromLane];
  return laneColor(lane.color);
}

export const KIND_LABELS: Record<TransitionDto["kind"], string> = {
  fork: "Dal ayrılması",
  merge: "Birleşme (merge)",
  cherryPick: "Cherry-pick",
};
