<script lang="ts">
  // The map. Phase 5 draws the country scale: land, regions with their
  // cities as faint outlines, islands grouped into archipelagos, and the
  // JDK coast. Roads and the other two scales arrive in phase 6.
  import type { AtlasViewDto } from "../bindings/AtlasViewDto";
  import type { NodeDto } from "../bindings/NodeDto";
  import type { RectDto } from "../bindings/RectDto";
  import { count, instability } from "../format";
  import { Camera } from "./camera.svelte";
  import { fitLabel, headerOf, shortPackage } from "./labels";

  let { view }: { view: AtlasViewDto } = $props();

  const camera = new Camera();
  let svg = $state<SVGSVGElement | null>(null);
  let hovered = $state<NodeDto | null>(null);
  let pointer = $state({ x: 0, y: 0 });

  const regions = $derived(view.members.filter((n) => n.kind === "module"));
  const islands = $derived(view.neighbours.filter((n) => n.kind === "library" || n.kind === "unknown"));
  const ports = $derived(view.neighbours.filter((n) => n.kind === "jdk"));
  /** Stable colour per region: its position among the modules. */
  const colour = $derived(new Map(regions.map((r, i) => [r.key, i % 8])));

  // Fit the whole map whenever a new project arrives (the world changes).
  let fittedWorld = "";
  $effect(() => {
    const key = `${view.world.x},${view.world.y},${view.world.w},${view.world.h}`;
    if (key !== fittedWorld) {
      fittedWorld = key;
      camera.setWorld(view.world);
      camera.fit(view.world, 0);
    }
  });

  // -- pointer → world ------------------------------------------------------

  function toWorld(clientX: number, clientY: number) {
    const ctm = svg?.getScreenCTM();
    if (!svg || !ctm) return { x: 0, y: 0 };
    const p = new DOMPoint(clientX, clientY).matrixTransform(ctm.inverse());
    return { x: p.x, y: p.y };
  }

  function onWheel(e: WheelEvent) {
    e.preventDefault();
    // Trackpads send many small deltas, mouse wheels few large ones: exp() treats both smoothly.
    camera.zoomAt(toWorld(e.clientX, e.clientY), Math.exp(e.deltaY * 0.0015));
  }

  // Dragging starts only after the pointer travelled a few pixels: a plain
  // click or double click must still reach the region under it. (Capturing
  // the pointer on press would retarget those events to the <svg>.)
  const DRAG_THRESHOLD = 4;
  let press: { x: number; y: number; id: number } | null = null;
  let drag = $state<{ x: number; y: number } | null>(null);
  let container = $state<HTMLDivElement | null>(null);

  function onPointerDown(e: PointerEvent) {
    if (e.button !== 0) return;
    press = { x: e.clientX, y: e.clientY, id: e.pointerId };
  }
  function onPointerMove(e: PointerEvent) {
    const box = container?.getBoundingClientRect();
    if (box) pointer = { x: e.clientX - box.left, y: e.clientY - box.top };
    if (!press || !svg) return;
    if (!drag) {
      if (Math.hypot(e.clientX - press.x, e.clientY - press.y) < DRAG_THRESHOLD) return;
      svg.setPointerCapture(press.id);
      drag = { x: press.x, y: press.y };
    }
    // Screen pixels → world units: the CTM's scale factor.
    const scale = svg.getScreenCTM()?.a ?? 1;
    camera.panBy((e.clientX - drag.x) / scale, (e.clientY - drag.y) / scale);
    drag = { x: e.clientX, y: e.clientY };
  }
  function onPointerUp() {
    press = null;
    drag = null;
  }

  // Our own double-click detection: the native `dblclick` is not delivered
  // reliably on SVG groups by every WebView (WebKitGTK swallowed it in tests).
  let lastClick: { key: string; at: number } | null = null;
  function regionClick(r: NodeDto) {
    const now = performance.now();
    if (lastClick?.key === r.key && now - lastClick.at < 400) {
      if (r.rect) camera.fit(r.rect);
      lastClick = null;
    } else {
      lastClick = { key: r.key, at: now };
    }
  }

  function zoomButton(factor: number) {
    const b = camera.box;
    camera.zoomAt({ x: b.x + b.w / 2, y: b.y + b.h / 2 }, factor);
  }

  function onKey(e: KeyboardEvent) {
    if (e.key === "+" || e.key === "=") zoomButton(0.8);
    else if (e.key === "-") zoomButton(1.25);
    else if (e.key === "0") camera.fit(view.world, 0);
    else return;
    e.preventDefault();
  }

  // -- drawing helpers ------------------------------------------------------

  function label(r: RectDto, text: string) {
    const h = headerOf(r);
    const pad = Math.min(8, Math.max(1, Math.min(r.w, r.h) * 0.03));
    return { ...fitLabel(text, r.w - 2 * pad, h * 0.8), x: r.x + pad * 1.5, y: r.y + pad + h * 0.75 };
  }

  /** Package keys are `p:<module>|<package>`. */
  function cityName(key: string): string {
    return key.slice(key.indexOf("|") + 1);
  }

  function islandShape(r: RectDto) {
    return { cx: r.x + r.w / 2, cy: r.y + r.h / 2, rx: r.w / 2, ry: (r.h / 2) * 0.82 };
  }

  const matchText: Record<string, string> = { jar: "jar ile doğrulandı", name: "ad benzerliğinden tahmin", mixed: "jar + tahmin" };
</script>

<div class="atlas" bind:this={container}>
  <!-- The map is a pan/zoom surface: it needs focus for + − 0 and pointer handlers. -->
  <!-- svelte-ignore a11y_no_noninteractive_tabindex, a11y_no_noninteractive_element_interactions -->
  <svg
    role="application"
    aria-label="Bağımlılık haritası; tekerlek ile yakınlaş, sürükleyerek kaydır, + − 0 tuşları"
    bind:this={svg}
    viewBox={camera.viewBox}
    preserveAspectRatio="xMidYMid meet"
    tabindex="0"
    onwheel={onWheel}
    onpointerdown={onPointerDown}
    onpointermove={onPointerMove}
    onpointerup={onPointerUp}
    onpointercancel={onPointerUp}
    onpointerleave={() => (hovered = null)}
    onkeydown={onKey}
    class:dragging={drag !== null}
  >
    <defs>
      <pattern id="hatch" width="6" height="6" patternUnits="userSpaceOnUse" patternTransform="rotate(45)">
        <line x1="0" y1="0" x2="0" y2="6" class="hatch-line" />
      </pattern>
      <filter id="land-shadow" x="-5%" y="-5%" width="110%" height="110%">
        <feDropShadow dx="0" dy="3" stdDeviation="6" flood-opacity="0.18" />
      </filter>
    </defs>

    <!-- The sea: larger than the world so panning never shows an edge. -->
    <rect class="sea" x={view.world.x - view.world.w * 4} y={view.world.y - view.world.h * 4} width={view.world.w * 9} height={view.world.h * 9} />

    <!-- The mainland (JDK) -->
    {#if view.mainland}
      {@const m = view.mainland}
      <rect class="mainland" x={m.x} y={m.y} width={m.w} height={m.h} rx="10" filter="url(#land-shadow)" />
      <text class="coast-name" x={m.x + 10} y={m.y + 20} font-size="12">JDK</text>
      {#each ports as p (p.key)}
        {#if p.rect}
          <g class="port" role="img" aria-label={p.name} onpointerover={() => (hovered = p)}>
            <rect x={p.rect.x} y={p.rect.y} width={p.rect.w} height={p.rect.h} rx="3" />
            <text x={p.rect.x + 6} y={p.rect.y + p.rect.h * 0.68} font-size={Math.min(10, p.rect.h * 0.55)}>{p.name}</text>
          </g>
        {/if}
      {/each}
    {/if}

    <!-- The land -->
    <rect class="land" x={view.land.x} y={view.land.y} width={view.land.w} height={view.land.h} rx="14" filter="url(#land-shadow)" />

    <!-- Regions -->
    {#each regions as r (r.key)}
      {#if r.rect}
        {@const t = label(r.rect, r.name)}
        <g
          class="region"
          class:hovered={hovered?.key === r.key}
          style:--fill="var(--region-{colour.get(r.key)})"
          onpointerover={() => (hovered = r)}
          onclick={() => regionClick(r)}
          onkeydown={(e) => e.key === "Enter" && r.rect && camera.fit(r.rect)}
          onfocus={() => (hovered = r)}
          role="button"
          tabindex="0"
          aria-label="{r.name} bölgesi; Enter ile yaklaş"
        >
          <rect class="fill" x={r.rect.x} y={r.rect.y} width={r.rect.w} height={r.rect.h} rx="6" />
          {#each view.outlines.filter((o) => o.parent === r.key) as c (c.key)}
            {@const ct = label(c.rect, shortPackage(cityName(c.key)))}
            <rect class="city" class:test={c.isTest} x={c.rect.x} y={c.rect.y} width={c.rect.w} height={c.rect.h} rx="3" />
            {#if ct.size >= 5}
              <text class="city-name" x={ct.x} y={ct.y} font-size={ct.size}>{ct.text}</text>
            {/if}
          {/each}
          {#if r.jpms}
            <!-- A customs border: a thick line with a thin inner line. -->
            <rect class="border jpms-outer" x={r.rect.x} y={r.rect.y} width={r.rect.w} height={r.rect.h} rx="6" />
            <rect class="border jpms-inner" x={r.rect.x + 2.5} y={r.rect.y + 2.5} width={r.rect.w - 5} height={r.rect.h - 5} rx="4" />
          {:else}
            <rect class="border soft" x={r.rect.x} y={r.rect.y} width={r.rect.w} height={r.rect.h} rx="6" />
          {/if}
          <text class="region-name" x={t.x} y={t.y} font-size={t.size}>{t.text}{r.inCycle ? " ⟲" : ""}</text>
        </g>
      {/if}
    {/each}

    <!-- Archipelagos and islands -->
    {#each view.archipelagos as a (a.name)}
      <rect class="archipelago" x={a.frame.x} y={a.frame.y} width={a.frame.w} height={a.frame.h} rx="12" />
      {@const at = fitLabel(a.name === "?" ? "bilinmeyen paketler" : a.name, a.frame.w - 20, 14, 8)}
      <text class="archipelago-name" x={a.frame.x + 10} y={a.frame.y + 22} font-size={at.size}>{at.text}</text>
    {/each}
    {#each islands as i (i.key)}
      {#if i.rect}
        {@const s = islandShape(i.rect)}
        {@const it = fitLabel(i.kind === "unknown" ? shortPackage(i.name) : i.name, i.rect.w + 24, 14, 8)}
        <g
          class="island"
          class:unused={i.kind === "library" && !i.matchedBy}
          class:unknown={i.kind === "unknown"}
          class:hovered={hovered?.key === i.key}
          onpointerover={() => (hovered = i)}
          role="img"
          aria-label={i.name}
        >
          <ellipse cx={s.cx} cy={s.cy} rx={s.rx} ry={s.ry} />
          <text class="island-name" x={s.cx} y={s.cy + s.ry + 18} font-size={it.size} text-anchor="middle">{it.text}
          </text>
        </g>
      {/if}
    {/each}
  </svg>

  <div class="controls">
    <button class="ghost" onclick={() => zoomButton(0.8)} aria-label="Yakınlaştır" title="Yakınlaştır (+)">+</button>
    <button class="ghost" onclick={() => zoomButton(1.25)} aria-label="Uzaklaştır" title="Uzaklaştır (−)">−</button>
    <button class="ghost" onclick={() => camera.fit(view.world, 0)} aria-label="Haritayı sığdır" title="Sığdır (0)">⤢</button>
  </div>

  {#if hovered && !drag}
    <div class="tooltip" style:left="{pointer.x + 14}px" style:top="{pointer.y + 14}px">
      <strong>{hovered.name}</strong>
      {#if hovered.kind === "module"}
        <div>{count(hovered.size)} satır{hovered.jpms ? ` · JPMS: ${hovered.jpms}` : ""}</div>
        {#if hovered.metrics}
          <div>Ca {hovered.metrics.ca} · Ce {hovered.metrics.ce} · I {instability(hovered.metrics.instability)}</div>
        {/if}
        {#if hovered.inCycle}<div class="bad">Paket döngüsü içeriyor</div>{/if}
        <div class="muted">Çift tıkla: yaklaş</div>
      {:else if hovered.kind === "library"}
        <div class="muted">{hovered.fqn}:{hovered.version ?? "?"}</div>
        <div>{hovered.matchedBy ? matchText[hovered.matchedBy] : "Kodda kullanılmıyor"}</div>
      {:else if hovered.kind === "unknown"}
        <div>Hiçbir beyan edilmiş kütüphane bu paketi açıklamıyor</div>
      {:else if hovered.kind === "jdk"}
        <div class="muted">JDK paketi</div>
      {/if}
    </div>
  {/if}
</div>

<style>
  .atlas { position: relative; height: 100%; min-height: 0; overflow: hidden; }
  svg { display: block; width: 100%; height: 100%; cursor: grab; touch-action: none; outline: none; user-select: none; -webkit-user-select: none; }
  svg.dragging { cursor: grabbing; }

  /* Every stroke keeps its pixel width at any zoom. */
  svg :global(rect), svg :global(ellipse), svg :global(line) { vector-effect: non-scaling-stroke; }

  .sea { fill: var(--sea); }
  .land { fill: var(--land); stroke: var(--land-edge); stroke-width: 1.5; }
  .mainland { fill: var(--land); stroke: var(--land-edge); stroke-width: 1.5; opacity: 0.85; }
  .coast-name, .archipelago-name { fill: var(--muted); font-weight: 600; letter-spacing: 0.04em; }
  .port rect { fill: var(--panel); stroke: var(--land-edge); }
  .port text { fill: var(--ink); }

  .region .fill { fill: var(--fill); }
  .region .city { fill: color-mix(in srgb, var(--panel) 38%, transparent); stroke: color-mix(in srgb, var(--ink) 14%, transparent); stroke-width: 0.75; }
  .region .city.test { fill: url(#hatch); }
  .hatch-line { stroke: color-mix(in srgb, var(--ink) 30%, transparent); stroke-width: 1.2; }
  .border { fill: none; }
  .border.soft { stroke: var(--border-ink); stroke-width: 1; stroke-dasharray: 5 3; }
  .border.jpms-outer { stroke: var(--border-ink); stroke-width: 3; }
  .border.jpms-inner { stroke: var(--border-ink); stroke-width: 1; }
  .city-name { fill: var(--muted); pointer-events: none; }
  .region-name { fill: var(--ink); font-weight: 600; paint-order: stroke; stroke: var(--fill); stroke-width: 3px; pointer-events: none; }
  .region.hovered .fill { filter: brightness(1.05); }
  .region.hovered .border { stroke: var(--accent); }
  .region:focus { outline: none; }
  .region:focus-visible .border { stroke: var(--accent); stroke-width: 3; }

  .archipelago { fill: none; stroke: color-mix(in srgb, var(--muted) 45%, transparent); stroke-width: 1; stroke-dasharray: 2 4; }
  .island ellipse { fill: var(--land); stroke: var(--land-edge); stroke-width: 1.5; }
  .island.unknown ellipse { stroke-dasharray: 4 3; fill: color-mix(in srgb, var(--land) 55%, var(--sea)); }
  .island.unused { opacity: 0.45; }
  .island.hovered ellipse { stroke: var(--accent); stroke-width: 2; }
  .island-name { fill: var(--ink); pointer-events: none; }

  .controls { position: absolute; right: 14px; top: 14px; display: flex; flex-direction: column; gap: 4px; }
  .controls button { width: 32px; height: 32px; padding: 0; background: var(--panel); font-size: 16px; }

  .tooltip {
    position: absolute;
    pointer-events: none;
    max-width: 280px;
    padding: 8px 10px;
    background: var(--panel);
    border: 1px solid var(--border);
    border-radius: 8px;
    box-shadow: 0 6px 18px rgb(0 0 0 / 0.16);
    font-size: 12px;
    line-height: 1.45;
    z-index: 5;
  }
  .muted { color: var(--muted); }
  .bad { color: var(--bad); }
</style>
