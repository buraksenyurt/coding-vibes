// Pure layout maths: column/row indices from Rust become pixels here.
// Keeping it free of Svelte makes it trivial to test and reuse.

import type { MetroMapDto } from "../bindings/MetroMapDto";

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

export function canvasSize(map: MetroMapDto): { width: number; height: number } {
  const rows = map.lanes.reduce((max, lane) => Math.max(max, lane.row + 1), 0);
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
