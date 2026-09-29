<script lang="ts">
  import { tick } from "svelte";
  import type { LaneDto } from "../bindings/LaneDto";
  import type { MetroMapDto } from "../bindings/MetroMapDto";
  import { view } from "../view.svelte";
  import Legend from "./Legend.svelte";
  import {
    COLUMN_WIDTH, HEADER_HEIGHT, LABEL_WIDTH, MERGE_RADIUS, ROW_HEIGHT, STATION_RADIUS,
    canvasSize, dateTicks, laneColor, trackSpan, transitionColor, transitionPath, x, y,
  } from "./geometry";

  let { map }: { map: MetroMapDto } = $props();

  let scroller: HTMLDivElement | undefined = $state();
  /** Index of the transition under the pointer, if any. */
  let hovered = $state<number | null>(null);

  const rowOf = $derived((lane: LaneDto) => (view.compact ? lane.compactRow : lane.row));
  const rowOfLane = $derived(map.lanes.map((lane) => rowOf(lane)));
  const size = $derived(canvasSize(map, rowOf));
  const ticks = $derived(dateTicks(map));
  const columnOf = $derived(new Map(map.commits.map((c, i) => [c.id, i])));
  const headColumn = $derived(map.head.kind === "unborn" ? undefined : columnOf.get(map.head.target));
  const tags = $derived(map.refs.filter((r) => r.kind === "tag"));
  const spans = $derived(map.lanes.map((lane) => trackSpan(lane, map.transitions)));

  /** Label column: one entry per row; in compact mode a row may hold several lanes. */
  const rows = $derived.by(() => {
    const byRow = new Map<number, LaneDto[]>();
    for (const lane of map.lanes) {
      const row = rowOf(lane);
      byRow.set(row, [...(byRow.get(row) ?? []), lane]);
    }
    return [...byRow.entries()]
      .sort(([a], [b]) => a - b)
      .map(([row, lanes]) => ({ row, lanes: lanes.sort((a, b) => b.lastColumn - a.lastColumn) }));
  });

  // What stays lit while a transition is hovered.
  const focus = $derived.by(() => {
    if (hovered === null) return null;
    const t = map.transitions[hovered];
    return { lanes: new Set([t.fromLane, t.toLane]), columns: new Set([t.fromColumn, t.toColumn]) };
  });
  const dimLane = (id: number) => focus !== null && !focus.lanes.has(id);
  const dimStation = (column: number) => focus !== null && !focus.columns.has(column);

  $effect(() => {
    void map;
    tick().then(() => {
      if (scroller) scroller.scrollLeft = scroller.scrollWidth;
    });
  });
</script>

<div class="scroller" bind:this={scroller}>
  <div class="canvas" style:height="{size.height}px">
    <div class="labels" style:width="{LABEL_WIDTH}px">
      {#each rows as { row, lanes } (row)}
        <div class="label" style:top="{y(row) - ROW_HEIGHT / 2}px" style:height="{ROW_HEIGHT}px">
          {#each lanes.slice(0, view.compact ? 3 : 1) as lane, i (lane.id)}
            <div class="entry" class:inferred={lane.labelKind !== "named"} class:secondary={i > 0}
                 class:dimmed={dimLane(lane.id)} title={lane.refs.join(", ") || lane.label}>
              <span class="swatch" style:background={laneColor(lane.color)}></span>
              <span class="text">
                <span class="name">{lane.label}</span>
                {#if !view.compact && lane.labelKind === "inferred"}<span class="note">silinmiş dal</span>{/if}
                {#if !view.compact && lane.labelKind === "anonymous"}<span class="note">adı bilinmeyen dal</span>{/if}
              </span>
            </div>
          {/each}
          {#if view.compact && lanes.length > 3}<span class="more">+{lanes.length - 3}</span>{/if}
        </div>
      {/each}
    </div>

    <svg width={size.width} height={size.height} role="img" aria-label="Dalların metro haritası">
      <defs>
        <marker id="arrow" viewBox="0 0 8 8" refX="7" refY="4" markerWidth="6" markerHeight="6" orient="auto">
          <path d="M 0 0 L 8 4 L 0 8 z" fill="context-stroke" />
        </marker>
      </defs>

      <!-- 1. Date ruler -->
      <g class="ruler">
        {#each ticks as t (t.column)}
          <line x1={x(t.column)} x2={x(t.column)} y1={HEADER_HEIGHT - 6} y2={size.height} />
          <text x={x(t.column) + 4} y={HEADER_HEIGHT - 12}>{t.label}</text>
        {/each}
      </g>

      <!-- 2. Lane tracks -->
      <g class="tracks">
        {#each map.lanes as lane (lane.id)}
          <line
            x1={x(spans[lane.id].from)} x2={x(spans[lane.id].to)}
            y1={y(rowOfLane[lane.id])} y2={y(rowOfLane[lane.id])}
            stroke={laneColor(lane.color)}
            class:inferred={lane.labelKind !== "named"}
            class:dimmed={dimLane(lane.id)}
          />
          {#if view.compact}
            <text class="inline-label" class:dimmed={dimLane(lane.id)}
                  x={x(spans[lane.id].from)} y={y(rowOfLane[lane.id]) - 9}>{lane.label}</text>
          {/if}
        {/each}
      </g>

      <!-- 3. Transitions -->
      <g class="transitions">
        {#each map.transitions as t, i (i)}
          {@const d = transitionPath(t, rowOfLane[t.fromLane], rowOfLane[t.toLane])}
          <path
            {d}
            class="transition {t.kind}"
            class:dimmed={focus !== null && hovered !== i}
            class:lit={hovered === i}
            stroke={transitionColor(t, map.lanes, view.colorMode)}
            marker-end={t.kind === "merge" ? "url(#arrow)" : undefined}
          />
          <!-- Wide invisible twin: a 2px line is too thin to hover. -->
          <path {d} class="hit" role="presentation"
                onpointerenter={() => (hovered = i)} onpointerleave={() => (hovered = null)} />
        {/each}
      </g>

      <!-- 4. Stations -->
      <g class="stations">
        {#each map.commits as commit, column (commit.id)}
          {@const lane = map.lanes[commit.lane]}
          {@const cx = x(column)}
          {@const cy = y(rowOfLane[commit.lane])}
          <g class:dimmed={dimStation(column)}>
            {#if commit.isMerge}
              <circle {cx} {cy} r={MERGE_RADIUS} class="merge-outer" stroke={laneColor(lane.color)} />
              <circle {cx} {cy} r={MERGE_RADIUS - 3.5} fill={laneColor(lane.color)} class="dot" />
            {:else}
              <circle {cx} {cy} r={STATION_RADIUS} fill={laneColor(lane.color)} class="dot" />
            {/if}
          </g>
        {/each}
      </g>

      <!-- 5. Markers: tags and HEAD -->
      <g class="markers" class:dimmed={focus !== null}>
        {#each tags as tag (tag.name)}
          {@const cx = x(tag.column)}
          {@const cy = y(rowOfLane[map.commits[tag.column].lane])}
          <line class="flagpole" x1={cx} x2={cx} y1={cy - 8} y2={cy - 17} />
          <g transform="translate({cx}, {cy - 17})">
            <rect class="flag" x="0" y="-11" width={tag.name.length * 6.5 + 10} height="13" rx="2" />
            <text class="flag-text" x="5" y="-1.5">{tag.name}</text>
          </g>
        {/each}
        {#if headColumn !== undefined}
          {@const cx = x(headColumn)}
          {@const cy = y(rowOfLane[map.commits[headColumn].lane])}
          <circle class="head-ring" cx={cx} cy={cy} r="10" />
          <text class="head-text" x={cx + 14} y={cy + 4}>HEAD</text>
        {/if}
      </g>
    </svg>
  </div>
</div>
<Legend />

<style>
  .scroller {
    position: absolute;
    inset: 0;
    overflow: auto;
    background: var(--surface);
  }
  .canvas {
    display: flex;
    width: max-content;
    min-width: 100%;
    position: relative;
  }
  .labels {
    position: sticky;
    left: 0;
    z-index: 2;
    flex: none;
    background: var(--surface);
    border-right: 1px solid var(--border);
  }
  .label {
    position: absolute;
    left: 0;
    right: 0;
    display: flex;
    flex-direction: column;
    justify-content: center;
    padding: 0 12px;
    overflow: hidden;
  }
  .entry { display: flex; align-items: center; gap: 8px; white-space: nowrap; min-width: 0; line-height: 1.25; }
  .entry.secondary { font-size: 11px; color: var(--text-secondary); }
  .text { display: flex; flex-direction: column; min-width: 0; }
  .entry .name { overflow: hidden; text-overflow: ellipsis; }
  .entry.inferred .name { font-style: italic; color: var(--text-secondary); }
  .note { font-size: 11px; color: var(--muted); }
  .more { font-size: 11px; color: var(--muted); padding-left: 22px; }
  .swatch { flex: none; width: 14px; height: 4px; border-radius: 2px; }
  .entry.inferred .swatch { opacity: 0.45; }

  svg { flex: none; display: block; }
  .ruler line { stroke: var(--grid); stroke-width: 1; }
  .ruler text { fill: var(--muted); font-size: 11px; }
  .tracks line { stroke-width: 3; stroke-linecap: round; }
  .tracks line.inferred { opacity: 0.45; }
  .inline-label { fill: var(--text-secondary); font-size: 10px; }

  .transition { fill: none; stroke-width: 2; stroke-linecap: round; transition: opacity 120ms; }
  .transition.cherryPick { stroke-dasharray: 5 4; }
  .transition.lit { stroke-width: 3; }
  .hit { fill: none; stroke: transparent; stroke-width: 12; pointer-events: stroke; cursor: default; }

  .dot { stroke: var(--surface); stroke-width: 2; }
  .merge-outer { fill: var(--surface); stroke-width: 2; }
  .flagpole { stroke: var(--muted); stroke-width: 1; }
  .flag { fill: var(--surface); stroke: var(--muted); stroke-width: 1; }
  .flag-text { fill: var(--text-secondary); font-size: 10px; font-weight: 600; }
  .head-ring { fill: none; stroke: var(--text); stroke-width: 1.5; animation: pulse 2s ease-in-out infinite; }
  .head-text { fill: var(--text); font-size: 10px; font-weight: 700; letter-spacing: 0.04em; }
  @keyframes pulse { 50% { stroke-opacity: 0.25; } }
  @media (prefers-reduced-motion: reduce) { .head-ring { animation: none; } }

  .dimmed { opacity: 0.15; transition: opacity 120ms; }
</style>
