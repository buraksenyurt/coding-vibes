<script lang="ts">
  import { tick } from "svelte";
  import type { MetroMapDto } from "../bindings/MetroMapDto";
  import {
    HEADER_HEIGHT, LABEL_WIDTH, MERGE_RADIUS, ROW_HEIGHT, STATION_RADIUS,
    canvasSize, dateTicks, laneColor, x, y,
  } from "./geometry";

  let { map }: { map: MetroMapDto } = $props();

  let scroller: HTMLDivElement | undefined = $state();

  const size = $derived(canvasSize(map));
  const ticks = $derived(dateTicks(map));
  const rowOfLane = $derived(map.lanes.map((lane) => lane.row));
  const columnOf = $derived(new Map(map.commits.map((c, i) => [c.id, i])));
  const headColumn = $derived(map.head.kind === "unborn" ? undefined : columnOf.get(map.head.target));
  const tags = $derived(map.refs.filter((r) => r.kind === "tag"));
  const lanesByRow = $derived([...map.lanes].sort((a, b) => a.row - b.row));

  // New map: jump to the newest commits on the right.
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
      {#each lanesByRow as lane (lane.id)}
        <div
          class="label"
          class:inferred={lane.labelKind !== "named"}
          style:top="{y(lane.row) - ROW_HEIGHT / 2}px"
          style:height="{ROW_HEIGHT}px"
          title={lane.refs.join(", ") || lane.label}
        >
          <span class="swatch" style:background={laneColor(lane.color)}></span>
          <span class="text">
            <span class="name">{lane.label}</span>
            {#if lane.labelKind === "inferred"}<span class="note">silinmiş dal</span>{/if}
            {#if lane.labelKind === "anonymous"}<span class="note">adı bilinmeyen dal</span>{/if}
          </span>
        </div>
      {/each}
    </div>

    <svg width={size.width} height={size.height} role="img" aria-label="Dalların metro haritası">
      <!-- 1. Date ruler and faint column guides -->
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
            x1={x(lane.firstColumn)} x2={x(lane.lastColumn)}
            y1={y(lane.row)} y2={y(lane.row)}
            stroke={laneColor(lane.color)}
            class:inferred={lane.labelKind !== "named"}
          />
        {/each}
      </g>

      <!-- 3. Stations -->
      <g class="stations">
        {#each map.commits as commit, column (commit.id)}
          {@const lane = map.lanes[commit.lane]}
          {@const cx = x(column)}
          {@const cy = y(rowOfLane[commit.lane])}
          {#if commit.isMerge}
            <circle {cx} {cy} r={MERGE_RADIUS} class="merge-outer" stroke={laneColor(lane.color)} />
            <circle {cx} {cy} r={MERGE_RADIUS - 3.5} fill={laneColor(lane.color)} class="dot" />
          {:else}
            <circle {cx} {cy} r={STATION_RADIUS} fill={laneColor(lane.color)} class="dot" />
          {/if}
        {/each}
      </g>

      <!-- 4. Markers: tags and HEAD -->
      <g class="markers">
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
    align-items: center;
    gap: 8px;
    padding: 0 12px;
    white-space: nowrap;
    color: var(--text);
  }
  .text { display: flex; flex-direction: column; min-width: 0; line-height: 1.2; }
  .label .name { overflow: hidden; text-overflow: ellipsis; }
  .label.inferred .name { font-style: italic; color: var(--text-secondary); }
  .note { font-size: 11px; color: var(--muted); }
  .swatch { flex: none; width: 14px; height: 4px; border-radius: 2px; }
  .label.inferred .swatch { opacity: 0.45; }

  svg { flex: none; display: block; }
  .ruler line { stroke: var(--grid); stroke-width: 1; }
  .ruler text { fill: var(--muted); font-size: 11px; }
  .tracks line { stroke-width: 3; stroke-linecap: round; }
  .tracks line.inferred { opacity: 0.45; }
  .dot { stroke: var(--surface); stroke-width: 2; }
  .merge-outer { fill: var(--surface); stroke-width: 2; }
  .flagpole { stroke: var(--muted); stroke-width: 1; }
  .flag { fill: var(--surface); stroke: var(--muted); stroke-width: 1; }
  .flag-text { fill: var(--text-secondary); font-size: 10px; font-weight: 600; }
  .head-ring { fill: none; stroke: var(--text); stroke-width: 1.5; animation: pulse 2s ease-in-out infinite; }
  .head-text { fill: var(--text); font-size: 10px; font-weight: 700; letter-spacing: 0.04em; }
  @keyframes pulse { 50% { stroke-opacity: 0.25; } }
  @media (prefers-reduced-motion: reduce) { .head-ring { animation: none; } }
</style>
