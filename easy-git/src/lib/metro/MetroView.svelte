<script lang="ts">
  import { tick } from "svelte";
  import type { LaneDto } from "../bindings/LaneDto";
  import type { MetroMapDto } from "../bindings/MetroMapDto";
  import { ago } from "../format";
  import { repo } from "../repo.svelte";
  import { view } from "../view.svelte";
  import Legend from "./Legend.svelte";
  import Tooltip from "./Tooltip.svelte";
  import {
    COLUMN_WIDTH, HEADER_HEIGHT, LABEL_WIDTH, MERGE_RADIUS, ROW_HEIGHT, STATION_RADIUS,
    KIND_LABELS, arrowHead, canvasSize, dateTicks, laneColor, trackSpan, transitionColor, transitionPath, x, y,
  } from "./geometry";

  let { map }: { map: MetroMapDto } = $props();

  let scroller: HTMLDivElement | undefined = $state();
  /** Index of the transition under the pointer, if any. */
  let hovered = $state<number | null>(null);
  /** Column of the station under the pointer, if any. */
  let hoveredStation = $state<number | null>(null);
  let pointer = $state({ x: 0, y: 0 });

  const hidden = $derived(repo.hiddenLanes);
  const visible = (lane: number) => !hidden.has(lane);

  // Rows of visible lanes, renumbered so hidden lanes leave no gaps.
  const rowOfLane = $derived.by(() => {
    const raw = map.lanes.map((lane) => (view.compact ? lane.compactRow : lane.row));
    const used = [...new Set(map.lanes.filter((l) => visible(l.id)).map((l) => raw[l.id]))].sort((a, b) => a - b);
    const renumber = new Map(used.map((row, i) => [row, i]));
    return raw.map((row) => renumber.get(row) ?? -1);
  });
  const rowOf = $derived((lane: LaneDto) => rowOfLane[lane.id]);
  const size = $derived(canvasSize(map, rowOf));
  const ticks = $derived(dateTicks(map));
  const columnOf = $derived(new Map(map.commits.map((c, i) => [c.id, i])));
  const headColumn = $derived(map.head.kind === "unborn" ? undefined : columnOf.get(map.head.target));
  const tags = $derived(map.refs.filter((r) => r.kind === "tag"));
  const spans = $derived(map.lanes.map((lane) => trackSpan(lane, map.transitions)));

  /** Label column: one entry per row; in compact mode a row may hold several lanes. */
  const rows = $derived.by(() => {
    const byRow = new Map<number, LaneDto[]>();
    for (const lane of map.lanes.filter((l) => visible(l.id))) {
      const row = rowOf(lane);
      byRow.set(row, [...(byRow.get(row) ?? []), lane]);
    }
    return [...byRow.entries()]
      .sort(([a], [b]) => a - b)
      .map(([row, lanes]) => ({ row, lanes: lanes.sort((a, b) => b.lastColumn - a.lastColumn) }));
  });

  // What stays lit: a hovered transition wins over a selected branch.
  const focus = $derived.by(() => {
    if (hovered !== null) {
      const t = map.transitions[hovered];
      return { lanes: new Set([t.fromLane, t.toLane]), columns: new Set([t.fromColumn, t.toColumn]) };
    }
    const sel = repo.selection;
    if (sel?.kind === "branch") {
      const lane = repo.stats.find((s) => s.name === sel.name)?.lane;
      if (lane === null || lane === undefined) return null;
      const columns = new Set(map.commits.flatMap((c, i) => (c.lane === lane ? [i] : [])));
      return { lanes: new Set([lane]), columns };
    }
    return null;
  });
  const selectedColumn = $derived(repo.selection?.kind === "commit" ? repo.selection.column : null);
  const shownTransitions = $derived(
    map.transitions.map((t, i) => ({ t, i })).filter(({ t }) => visible(t.fromLane) && visible(t.toLane)),
  );
  const dimLane = (id: number) => focus !== null && !focus.lanes.has(id);
  const dimStation = (column: number) => focus !== null && !focus.columns.has(column);

  $effect(() => {
    void map;
    tick().then(() => {
      if (scroller) scroller.scrollLeft = scroller.scrollWidth;
    });
  });

  // Someone asked to see a column (parent link, branch tip): centre it.
  $effect(() => {
    const target = repo.scrollTarget;
    if (target === null || !scroller) return;
    scroller.scrollTo({ left: x(target) - (scroller.clientWidth - LABEL_WIDTH) / 2, behavior: "smooth" });
    repo.scrollTarget = null;
  });

  function track(event: PointerEvent) {
    pointer = { x: event.clientX, y: event.clientY };
  }

  function onKey(event: KeyboardEvent) {
    if (event.key === "Escape") repo.clearSelection();
  }
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
      <!-- 1. Date ruler -->
      <g class="ruler">
        {#each ticks as t (t.column)}
          <line x1={x(t.column)} x2={x(t.column)} y1={HEADER_HEIGHT - 6} y2={size.height} />
          <text x={x(t.column) + 4} y={HEADER_HEIGHT - 12}>{t.label}</text>
        {/each}
      </g>

      <!-- 2. Lane tracks -->
      <g class="tracks">
        {#each map.lanes.filter((l) => visible(l.id)) as lane (lane.id)}
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
        {#each shownTransitions as { t, i } (i)}
          {@const d = transitionPath(t, rowOfLane[t.fromLane], rowOfLane[t.toLane])}
          <path
            {d}
            class="transition {t.kind}"
            class:dimmed={focus !== null && hovered !== i}
            class:lit={hovered === i}
            stroke={transitionColor(t, map.lanes, view.colorMode)}
          />
          {#if t.kind === "merge"}
            <path d={arrowHead(t.toColumn, rowOfLane[t.toLane])} class="arrow"
                  class:dimmed={focus !== null && hovered !== i}
                  fill={transitionColor(t, map.lanes, view.colorMode)} />
          {/if}
          <!-- Wide invisible twin: a 2px line is too thin to hover. -->
          <path {d} class="hit" role="presentation"
                onpointerenter={(e) => { hovered = i; track(e); }} onpointermove={track}
                onpointerleave={() => (hovered = null)} />
        {/each}
      </g>

      <!-- 4. Stations -->
      <g class="stations">
        {#each map.commits as commit, column (commit.id)}
          {#if visible(commit.lane)}
            {@const lane = map.lanes[commit.lane]}
            {@const cx = x(column)}
            {@const cy = y(rowOfLane[commit.lane])}
            <g class:dimmed={dimStation(column)}>
              {#if selectedColumn === column}
                <circle {cx} {cy} r="11" class="selected-ring" />
              {/if}
              {#if commit.isMerge}
                <circle {cx} {cy} r={MERGE_RADIUS} class="merge-outer" stroke={laneColor(lane.color)} />
                <circle {cx} {cy} r={MERGE_RADIUS - 3.5} fill={laneColor(lane.color)} class="dot" />
              {:else}
                <circle {cx} {cy} r={STATION_RADIUS} fill={laneColor(lane.color)} class="dot" />
              {/if}
              <!-- Hit target larger than the mark; a click selects the commit. -->
              <circle {cx} {cy} r="11" class="station-hit" role="button" tabindex="-1"
                      aria-label="{commit.shortId} {commit.summary}"
                      onpointerenter={(e) => { hoveredStation = column; track(e); }}
                      onpointermove={track}
                      onpointerleave={() => (hoveredStation = null)}
                      onclick={() => repo.selectCommit(column)}
                      onkeydown={(e) => e.key === "Enter" && repo.selectCommit(column)} />
            </g>
          {/if}
        {/each}
      </g>

      <!-- 5. Markers: tags and HEAD -->
      <g class="markers" class:dimmed={focus !== null}>
        {#each tags.filter((t) => visible(map.commits[t.column].lane)) as tag (tag.name)}
          {@const cx = x(tag.column)}
          {@const cy = y(rowOfLane[map.commits[tag.column].lane])}
          <line class="flagpole" x1={cx} x2={cx} y1={cy - 8} y2={cy - 17} />
          <g transform="translate({cx}, {cy - 17})">
            <rect class="flag" x="0" y="-11" width={tag.name.length * 6.5 + 10} height="13" rx="2" />
            <text class="flag-text" x="5" y="-1.5">{tag.name}</text>
          </g>
        {/each}
        {#if headColumn !== undefined && visible(map.commits[headColumn].lane)}
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

<svelte:window onkeydown={onKey} />

{#if hoveredStation !== null}
  {@const c = map.commits[hoveredStation]}
  <Tooltip x={pointer.x} y={pointer.y}>
    <div class="tip-head"><span class="mono">{c.shortId}</span> · {c.authorName} · {ago(c.time)}</div>
    <div class="tip-body">{c.summary}</div>
    <div class="tip-foot">{map.lanes[c.lane].label}{c.isMerge ? " · merge commit" : ""}</div>
  </Tooltip>
{:else if hovered !== null}
  {@const t = map.transitions[hovered]}
  <Tooltip x={pointer.x} y={pointer.y}>
    <div class="tip-head">{KIND_LABELS[t.kind]}</div>
    <div class="tip-body">{map.lanes[t.fromLane].label} → {map.lanes[t.toLane].label}</div>
    <div class="tip-foot">{map.commits[t.toColumn].shortId} · {ago(map.commits[t.toColumn].time)}</div>
  </Tooltip>
{/if}

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
  .station-hit { fill: transparent; cursor: pointer; outline: none; }
  .selected-ring { fill: none; stroke: var(--accent); stroke-width: 2; }

  .tip-head { color: var(--text-secondary); margin-bottom: 2px; }
  .tip-body { font-weight: 600; }
  .tip-foot { color: var(--muted); margin-top: 2px; }
  .mono { font-family: ui-monospace, "Cascadia Code", Consolas, monospace; }
</style>
