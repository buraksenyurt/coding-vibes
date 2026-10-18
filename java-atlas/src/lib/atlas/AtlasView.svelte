<script lang="ts">
  // The map. The country view is the base map and is always drawn; the view
  // of the current scale (region or city) is drawn on top of it, with roads.
  // Zooming far enough in or out changes the scale (semantic zoom).
  import type { NodeDto } from "../bindings/NodeDto";
  import type { RectDto } from "../bindings/RectDto";
  import type { ViewEdgeDto } from "../bindings/ViewEdgeDto";
  import type { TypeKindDto } from "../bindings/TypeKindDto";
  import { count, instability } from "../format";
  import { project } from "../project.svelte";
  import { Camera } from "./camera.svelte";
  import { fitLabel, headerOf, shortPackage } from "./labels";
  import { arrowHead, road, roadWidth, type Shape } from "./roads";
  import Breadcrumb from "./Breadcrumb.svelte";
  import Legend from "./Legend.svelte";

  const base = $derived(project.country!);
  const view = $derived(project.view ?? base);
  const scale = $derived(project.scale);
  const focusKey = $derived(project.focus.key);

  const camera = new Camera();
  let svg = $state<SVGSVGElement | null>(null);
  let container = $state<HTMLDivElement | null>(null);
  let width = $state(800);
  let height = $state(600);
  let hovered = $state<NodeDto | null>(null);
  let hoveredRoad = $state<ViewEdgeDto | null>(null);
  let pointer = $state({ x: 0, y: 0 });

  // -- what to draw -----------------------------------------------------------

  const regions = $derived(base.members);
  const colour = $derived(new Map(regions.map((r, i) => [r.key, i % 8])));
  /** The module whose inside is shown at region and city scale. */
  const focusRegion = $derived(
    scale === "region" ? focusKey : scale === "city" ? (project.crumbs[1]?.key ?? null) : null,
  );
  const cities = $derived(scale === "region" ? view.members : []);
  const buildings = $derived(scale === "city" ? view.members : []);
  /** Every node with a place on the map, by key: endpoints of roads. */
  const nodes = $derived(
    new Map([...base.members, ...base.neighbours, ...view.members, ...view.neighbours].map((n) => [n.key, n])),
  );
  /** Islands come from the base map, so they never jump; unconnected ones fade at closer scales. */
  const islands = $derived(base.neighbours.filter((n) => n.kind === "library" || n.kind === "unknown"));
  const connected = $derived(new Set(view.edges.flatMap((e) => [e.from, e.to])));
  const ports = $derived(view.neighbours.filter((n) => n.kind === "jdk"));
  const mainland = $derived(view.mainland ?? (project.filters.jdk ? base.mainland : null));

  /** Pixels on the right covered by the details drawer (see App.svelte). */
  const covered = $derived(project.selection ? Math.min(380, width * 0.45) : 0);
  /** Every flight keeps its target out from under the drawer. */
  function flyTo(r: RectDto, margin = 0.04) {
    camera.flyTo(r, 450, margin, { width, height, covered });
  }

  /** World units per screen pixel ("meet" fits the box inside the element). */
  const unit = $derived(Math.max(camera.box.w / width, camera.box.h / height));

  function shapeOf(n: NodeDto): Shape {
    return n.kind === "library" || n.kind === "unknown" ? "ellipse" : "rect";
  }

  const roads = $derived(
    view.edges.flatMap((e) => {
      const a = nodes.get(e.from);
      const b = nodes.get(e.to);
      if (!a?.rect || !b?.rect) return [];
      const kind = b.kind === "jdk" ? "coast" : b.kind === "library" || b.kind === "unknown" ? "ferry" : "road";
      return [{ edge: e, kind, geometry: road(a.rect, shapeOf(a), b.rect, shapeOf(b)), width: roadWidth(e.main + e.test) }];
    }),
  );

  // -- selection ----------------------------------------------------------------

  const selectedKey = $derived(project.selection?.kind === "node" ? project.selection.details.node.key : null);
  const selectedRoad = $derived(project.selection?.kind === "road" ? project.selection.edge : null);
  /** The selected node as drawn here, or as the details panel knows it (a type at region scale). */
  const selectedNode = $derived(
    project.selection?.kind === "node" ? (nodes.get(project.selection.details.node.key) ?? project.selection.details.node) : null,
  );

  function isSelectedRoad(e: ViewEdgeDto): boolean {
    return selectedRoad !== null && selectedRoad.from === e.from && selectedRoad.to === e.to;
  }

  /**
   * Hover: outgoing roads blue, incoming orange, the rest faded. Without a
   * hover the selected node does the same, a little softer, so that its
   * roads stay visible while the pointer explores the details panel.
   */
  function roadState(e: ViewEdgeDto): string {
    const n = hovered ?? selectedNode;
    if (!n) return selectedRoad ? (isSelectedRoad(e) ? "" : "dim") : "";
    if (e.from === n.key || contains(n, e.from)) return "out";
    if (e.to === n.key || contains(n, e.to)) return "in";
    return hovered ? "faded" : "dim";
  }
  /** Is `key` the node itself or something inside it (a type of a hovered city)? */
  function contains(n: { key: string }, key: string): boolean {
    let current = nodes.get(key);
    while (current) {
      if (current.key === n.key) return true;
      current = current.parent ? nodes.get(current.parent) : undefined;
    }
    return false;
  }

  // A tooltip about something that is no longer drawn would be confusing.
  $effect(() => {
    void project.crumbs;
    hovered = null;
    hoveredRoad = null;
  });

  // -- camera -----------------------------------------------------------------

  let fittedWorld = "";
  $effect(() => {
    const key = `${base.world.x},${base.world.y},${base.world.w},${base.world.h}`;
    if (key !== fittedWorld) {
      fittedWorld = key;
      camera.setWorld(base.world);
      camera.fit(base.world, 0);
    }
  });

  // Requests from the tree, the search box, the report and the details panel.
  let flown = 0;
  $effect(() => {
    const f = project.flight;
    if (f && f.seq !== flown) {
      flown = f.seq;
      flyTo(f.rect, f.margin);
    }
  });

  function toWorld(clientX: number, clientY: number) {
    const ctm = svg?.getScreenCTM();
    if (!svg || !ctm) return { x: 0, y: 0 };
    const p = new DOMPoint(clientX, clientY).matrixTransform(ctm.inverse());
    return { x: p.x, y: p.y };
  }

  // Semantic zoom: once the wheel rests, see whether the view now mostly shows
  // one region/city (go in) or is much wider than the focus (go out). The two
  // thresholds differ on purpose (hysteresis), so the scale never flickers.
  const ENTER_SHARE = 0.55;
  const LEAVE_FACTOR = 2.6;
  let settle: ReturnType<typeof setTimeout> | undefined;

  function checkScale() {
    const b = camera.box;
    const c = { x: b.x + b.w / 2, y: b.y + b.h / 2 };
    const inside = (r: RectDto | null) => r && c.x >= r.x && c.x <= r.x + r.w && c.y >= r.y && c.y <= r.y + r.h;
    const focus = project.focus;
    if (focus.rect && b.w > focus.rect.w * LEAVE_FACTOR) {
      project.up();
      return;
    }
    const candidates = scale === "country" ? regions : scale === "region" ? cities : [];
    const target = candidates.find((n) => inside(n.rect) && n.rect!.w >= b.w * ENTER_SHARE);
    if (target) project.enter(target);
  }

  function onWheel(e: WheelEvent) {
    e.preventDefault();
    // Trackpads send many small deltas, mouse wheels few large ones: exp() treats both smoothly.
    camera.zoomAt(toWorld(e.clientX, e.clientY), Math.exp(e.deltaY * 0.0015));
    clearTimeout(settle);
    settle = setTimeout(checkScale, 220);
  }

  /** Flies to a node and makes it the focus. */
  function enter(n: NodeDto) {
    if (n.rect) flyTo(n.rect);
    project.enter(n);
    // The tile that had focus disappears at the new scale; keep keys working.
    svg?.focus();
  }

  /** Goes up one or more scales and flies back out. */
  function goTo(index: number) {
    const target = project.crumbs[index];
    flyTo(target?.rect ?? base.world, target?.rect ? 0.04 : 0);
    project.goTo(index);
  }

  // Dragging starts only after the pointer travelled a few pixels, so that a
  // click still reaches the tile under it (pointer capture would retarget it).
  const DRAG_THRESHOLD = 4;
  let press: { x: number; y: number; id: number } | null = null;
  let drag = $state<{ x: number; y: number } | null>(null);

  /** Set when the last press became a drag: the click that follows it is not a click. */
  let dragged = false;

  function onPointerDown(e: PointerEvent) {
    if (e.button !== 0) return;
    press = { x: e.clientX, y: e.clientY, id: e.pointerId };
    dragged = false;
  }
  function onPointerMove(e: PointerEvent) {
    const box = container?.getBoundingClientRect();
    if (box) pointer = { x: e.clientX - box.left, y: e.clientY - box.top };
    if (!press || !svg) return;
    if (!drag) {
      if (Math.hypot(e.clientX - press.x, e.clientY - press.y) < DRAG_THRESHOLD) return;
      svg.setPointerCapture(press.id);
      drag = { x: press.x, y: press.y };
      dragged = true;
    }
    const scaleFactor = svg.getScreenCTM()?.a ?? 1;
    camera.panBy((e.clientX - drag.x) / scaleFactor, (e.clientY - drag.y) / scaleFactor);
    drag = { x: e.clientX, y: e.clientY };
  }
  function onPointerUp() {
    press = null;
    drag = null;
  }

  // Double clicks are detected by hand: WebKitGTK does not deliver `dblclick`
  // to SVG groups reliably.
  // One click selects (details panel), a second one within 400 ms enters.
  let lastClick: { key: string; at: number } | null = null;
  function tileClick(n: NodeDto) {
    if (dragged) return;
    const now = performance.now();
    if (lastClick?.key === n.key && now - lastClick.at < 400) {
      enter(n);
      lastClick = null;
    } else {
      lastClick = { key: n.key, at: now };
      if (selectedKey !== n.key) project.select(n.key);
    }
  }

  /** Buildings, islands and ports cannot be entered: a click only selects. */
  function pick(n: NodeDto) {
    if (!dragged && selectedKey !== n.key) project.select(n.key);
  }

  function roadClick(e: ViewEdgeDto) {
    const from = nodes.get(e.from);
    const to = nodes.get(e.to);
    if (!dragged && from && to) project.selectRoad(e, from, to);
  }

  /** A click on open sea or empty land closes the details panel. */
  function backgroundClick() {
    if (!dragged) project.clearSelection();
  }

  function zoomButton(factor: number) {
    const b = camera.box;
    camera.zoomAt({ x: b.x + b.w / 2, y: b.y + b.h / 2 }, factor);
    clearTimeout(settle);
    settle = setTimeout(checkScale, 220);
  }

  /** Esc and 0 work wherever the focus is, unless the user is typing. */
  function onWindowKey(e: KeyboardEvent) {
    const target = e.target as HTMLElement | null;
    if (target?.closest("input, textarea, select, [contenteditable]") || target?.closest("svg")) return;
    if (e.key === "Escape" || e.key === "0") onKey(e);
  }

  function onKey(e: KeyboardEvent) {
    if (e.key === "+" || e.key === "=") zoomButton(0.8);
    else if (e.key === "-") zoomButton(1.25);
    else if (e.key === "0") goTo(0);
    else if (e.key === "Escape" && project.selection) project.clearSelection();
    else if (e.key === "Escape" && project.crumbs.length > 1) goTo(project.crumbs.length - 2);
    else return;
    e.preventDefault();
  }

  // -- drawing helpers --------------------------------------------------------

  function label(r: RectDto, text: string, max?: number) {
    const h = headerOf(r);
    const pad = Math.min(8, Math.max(1, Math.min(r.w, r.h) * 0.03));
    return { ...fitLabel(text, r.w - 2 * pad, max ?? h * 0.8), x: r.x + pad * 1.5, y: r.y + pad + h * 0.75 };
  }

  /** Package keys are `p:<module>|<package>`. */
  function cityName(key: string): string {
    return key.slice(key.indexOf("|") + 1);
  }

  function islandShape(r: RectDto) {
    return { cx: r.x + r.w / 2, cy: r.y + r.h / 2, rx: r.w / 2, ry: (r.h / 2) * 0.82 };
  }

  /** Building outline by type kind, inside its tile. */
  function building(r: RectDto, kind: TypeKindDto | null): { tag: "rect" | "polygon"; rx?: number; points?: string; box: RectDto } {
    const m = Math.min(r.w, r.h) * 0.12;
    const b = { x: r.x + m, y: r.y + m, w: r.w - 2 * m, h: r.h - 2 * m };
    const cx = b.x + b.w / 2;
    const cy = b.y + b.h / 2;
    switch (kind) {
      case "enum": {
        const dx = b.w / 2;
        const dy = b.h / 2;
        const pts = [
          [cx - dx, cy], [cx - dx / 2, cy - dy], [cx + dx / 2, cy - dy],
          [cx + dx, cy], [cx + dx / 2, cy + dy], [cx - dx / 2, cy + dy],
        ];
        return { tag: "polygon", points: pts.map((p) => p.join(",")).join(" "), box: b };
      }
      case "annotation":
        return { tag: "polygon", points: `${cx},${b.y} ${b.x + b.w},${b.y + b.h} ${b.x},${b.y + b.h}`, box: b };
      case "record":
        return { tag: "rect", rx: Math.min(b.w, b.h) * 0.3, box: b };
      default:
        return { tag: "rect", rx: 2, box: b };
    }
  }

  const matchText: Record<string, string> = { jar: "jar ile doğrulandı", name: "ad benzerliğinden tahmin", mixed: "jar + tahmin" };
  const typeText: Record<TypeKindDto, string> = {
    class: "sınıf",
    interface: "arayüz",
    enum: "enum",
    record: "record",
    annotation: "annotation",
  };
</script>

<svelte:window onkeydown={onWindowKey} />

<div class="atlas" style:--covered="{covered}px" bind:this={container} bind:clientWidth={width} bind:clientHeight={height}>
  <!-- The map is a pan/zoom surface: it needs focus for + − 0 Esc and pointer handlers. -->
  <!-- svelte-ignore a11y_no_noninteractive_tabindex, a11y_no_noninteractive_element_interactions -->
  <svg
    role="application"
    aria-label="Bağımlılık haritası; tekerlek ile yakınlaş, sürükleyerek kaydır, + − 0 Esc tuşları"
    bind:this={svg}
    viewBox={camera.viewBox}
    preserveAspectRatio="xMidYMid meet"
    tabindex="0"
    onwheel={onWheel}
    onpointerdown={onPointerDown}
    onpointermove={onPointerMove}
    onpointerup={onPointerUp}
    onpointercancel={onPointerUp}
    onpointerleave={() => ((hovered = null), (hoveredRoad = null))}
    onkeydown={onKey}
    class:dragging={drag !== null}
    class:has-hover={hovered !== null}
    class:dense={roads.length > 60}
  >
    <defs>
      <pattern id="hatch" width="6" height="6" patternUnits="userSpaceOnUse" patternTransform="rotate(45)">
        <line x1="0" y1="0" x2="0" y2="6" class="hatch-line" />
      </pattern>
      <filter id="land-shadow" x="-5%" y="-5%" width="110%" height="110%">
        <feDropShadow dx="0" dy="3" stdDeviation="6" flood-opacity="0.18" />
      </filter>
    </defs>

    <!-- svelte-ignore a11y_click_events_have_key_events, a11y_no_static_element_interactions -->
    <rect class="sea" onclick={backgroundClick} x={base.world.x - base.world.w * 4} y={base.world.y - base.world.h * 4} width={base.world.w * 9} height={base.world.h * 9} />

    <!-- Mainland (JDK) -->
    {#if mainland}
      <rect class="mainland" x={mainland.x} y={mainland.y} width={mainland.w} height={mainland.h} rx="10" filter="url(#land-shadow)" />
      <text class="coast-name" x={mainland.x + 10} y={mainland.y + 20} font-size="12">JDK</text>
      {#each ports as p (p.key)}
        {#if p.rect}
          <!-- svelte-ignore a11y_click_events_have_key_events -->
          <g class="port" class:selected={selectedKey === p.key} role="img" aria-label={p.name} onpointerover={() => (hovered = p)} onclick={() => pick(p)}>
            <rect x={p.rect.x} y={p.rect.y} width={p.rect.w} height={p.rect.h} rx="3" />
            <text x={p.rect.x + 6} y={p.rect.y + p.rect.h * 0.68} font-size={Math.min(10, p.rect.h * 0.55)}>{p.name}</text>
          </g>
        {/if}
      {/each}
    {/if}

    <!-- svelte-ignore a11y_click_events_have_key_events, a11y_no_static_element_interactions -->
    <rect class="land" onclick={backgroundClick} x={base.land.x} y={base.land.y} width={base.land.w} height={base.land.h} rx="14" filter="url(#land-shadow)" />

    <!-- Regions: always the whole country; outside the focus they are context. -->
    {#each regions as r (r.key)}
      {#if r.rect}
        {@const t = label(r.rect, r.name)}
        {@const inFocus = focusRegion === r.key}
        <g
          class="region"
          class:context={focusRegion !== null && !inFocus}
          class:hovered={hovered?.key === r.key}
          class:selected={selectedKey === r.key}
          style:--fill="var(--region-{colour.get(r.key)})"
          onpointerover={() => (hovered = r)}
          onclick={() => tileClick(r)}
          onkeydown={(e) => e.key === "Enter" && enter(r)}
          onfocus={() => (hovered = r)}
          role="button"
          tabindex={scale === "country" ? 0 : -1}
          aria-label="{r.name} bölgesi; Enter ile yaklaş"
        >
          <rect class="fill" x={r.rect.x} y={r.rect.y} width={r.rect.w} height={r.rect.h} rx="6" />
          {#if !inFocus || scale === "city"}
            {#each base.outlines.filter((o) => o.parent === r.key && !(scale === "city" && o.key === focusKey)) as c (c.key)}
              {@const ct = label(c.rect, shortPackage(cityName(c.key)))}
              <rect class="city-outline" class:test={c.isTest} class:selected={selectedKey === c.key} x={c.rect.x} y={c.rect.y} width={c.rect.w} height={c.rect.h} rx="3" />
              {#if ct.size >= 5}
                <text class="city-name" x={ct.x} y={ct.y} font-size={ct.size}>{ct.text}</text>
              {/if}
            {/each}
          {/if}
          {#if r.jpms}
            <rect class="border jpms-outer" x={r.rect.x} y={r.rect.y} width={r.rect.w} height={r.rect.h} rx="6" />
            <rect class="border jpms-inner" x={r.rect.x + 2.5} y={r.rect.y + 2.5} width={r.rect.w - 5} height={r.rect.h - 5} rx="4" />
          {:else}
            <rect class="border soft" x={r.rect.x} y={r.rect.y} width={r.rect.w} height={r.rect.h} rx="6" />
          {/if}
          <text class="region-name" x={t.x} y={t.y} font-size={t.size}>{t.text}{r.inCycle ? " ⟲" : ""}</text>
        </g>
      {/if}
    {/each}

    <!-- Cities of the focused region (region scale) -->
    {#each cities as c (c.key)}
      {#if c.rect}
        {@const ct = label(c.rect, shortPackage(c.name), 22)}
        <g
          class="city"
          class:test={c.isTest}
          class:cycle={c.inCycle}
          class:hovered={hovered?.key === c.key}
          class:selected={selectedKey === c.key}
          onpointerover={() => (hovered = c)}
          onclick={() => tileClick(c)}
          onkeydown={(e) => e.key === "Enter" && enter(c)}
          onfocus={() => (hovered = c)}
          role="button"
          tabindex="0"
          aria-label="{c.name} şehri; Enter ile yaklaş"
        >
          <rect class="city-fill" x={c.rect.x} y={c.rect.y} width={c.rect.w} height={c.rect.h} rx="4" />
          {#each view.outlines.filter((o) => o.parent === c.key) as b (b.key)}
            <rect class="building-outline" class:selected={selectedKey === b.key} x={b.rect.x + b.rect.w * 0.1} y={b.rect.y + b.rect.h * 0.1} width={b.rect.w * 0.8} height={b.rect.h * 0.8} rx="1.5" />
          {/each}
          {#if c.exported === false}
            <!-- Not exported by its JPMS module: a locked city. -->
            <text class="lock" x={c.rect.x + c.rect.w - 4} y={c.rect.y + ct.size + 2} font-size={ct.size * 0.8} text-anchor="end">🔒</text>
          {/if}
          <text class="city-label" x={ct.x} y={ct.y} font-size={ct.size}>{ct.text}{c.inCycle ? " ⟲" : ""}</text>
        </g>
      {/if}
    {/each}

    <!-- Buildings of the focused city (city scale) -->
    {#if scale === "city" && project.focus.rect}
      {@const fr = project.focus.rect}
      {@const fl = label(fr, shortPackage(project.focus.name), 22)}
      <rect class="city-fill focus" x={fr.x} y={fr.y} width={fr.w} height={fr.h} rx="4" />
      <text class="city-label" x={fl.x} y={fl.y} font-size={fl.size}>{fl.text}</text>
    {/if}
    {#each buildings as b (b.key)}
      {#if b.rect}
        {@const shape = building(b.rect, b.typeKind)}
        {@const bl = fitLabel(b.name, shape.box.w * 0.9, Math.min(14, shape.box.h * 0.3), 2)}
        <g
          class="building {b.typeKind}"
          class:test={b.isTest}
          class:hovered={hovered?.key === b.key}
          class:selected={selectedKey === b.key}
          onpointerover={() => (hovered = b)}
          onclick={() => pick(b)}
          onkeydown={(e) => e.key === "Enter" && pick(b)}
          role="button"
          tabindex="0"
          aria-label="{b.name} ({b.typeKind ? typeText[b.typeKind] : ''})"
        >
          {#if shape.tag === "rect"}
            <rect x={shape.box.x} y={shape.box.y} width={shape.box.w} height={shape.box.h} rx={shape.rx} />
          {:else}
            <polygon points={shape.points} />
          {/if}
          {#if bl.size >= 2.5}
            <text x={shape.box.x + shape.box.w / 2} y={shape.box.y + shape.box.h / 2 + bl.size * 0.35} font-size={bl.size} text-anchor="middle">{bl.text}</text>
          {/if}
        </g>
      {/if}
    {/each}

    <!-- Archipelagos and islands (from the base map: they never move) -->
    {#each base.archipelagos as a (a.name)}
      {@const at = fitLabel(a.name === "?" ? "bilinmeyen paketler" : a.name, a.frame.w - 20, 14, 8)}
      <rect class="archipelago" x={a.frame.x} y={a.frame.y} width={a.frame.w} height={a.frame.h} rx="12" />
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
          class:context={scale !== "country" && !connected.has(i.key)}
          class:hovered={hovered?.key === i.key}
          class:selected={selectedKey === i.key}
          onpointerover={() => (hovered = i)}
          onclick={() => pick(i)}
          onkeydown={(e) => e.key === "Enter" && pick(i)}
          role="button"
          tabindex="0"
          aria-label={i.name}
        >
          <ellipse cx={s.cx} cy={s.cy} rx={s.rx} ry={s.ry} />
          <text class="island-name" x={s.cx} y={s.cy + s.ry + 18} font-size={it.size} text-anchor="middle">{it.text}</text>
        </g>
      {/if}
    {/each}

    <!-- Roads, on top of everything they connect -->
    <g class="roads">
      {#each roads as r (r.edge.from + ">" + r.edge.to)}
        {@const state = roadState(r.edge)}
        <g
          class="road {r.kind} {state}"
          class:cycle={r.edge.inCycle}
          class:violation={r.edge.violation}
          class:test-only={r.edge.main === 0}
          class:selected={isSelectedRoad(r.edge)}
          role="button"
          tabindex="-1"
          aria-label="{nodes.get(r.edge.from)?.name} → {nodes.get(r.edge.to)?.name}; tıkla: kanıtlar"
          onpointerover={() => (hoveredRoad = r.edge)}
          onpointerout={() => (hoveredRoad = null)}
          onclick={() => roadClick(r.edge)}
          onkeydown={(e) => e.key === "Enter" && roadClick(r.edge)}
        >
          {#if r.kind === "road"}
            <path class="casing" d={r.geometry.d} style:stroke-width="{r.width + 2.5}px" />
          {/if}
          <path class="line" d={r.geometry.d} style:stroke-width="{r.width}px" />
          <path class="arrow" d={arrowHead(r.geometry.mid, r.geometry.angle, (6 + r.width) * unit)} />
          <path class="hit" d={r.geometry.d} />
        </g>
      {/each}
    </g>
  </svg>

  <Breadcrumb crumbs={project.crumbs} onselect={goTo} />
  <Legend />

  <div class="controls">
    <button class="ghost" onclick={() => zoomButton(0.8)} aria-label="Yakınlaştır" title="Yakınlaştır (+)">+</button>
    <button class="ghost" onclick={() => zoomButton(1.25)} aria-label="Uzaklaştır" title="Uzaklaştır (−)">−</button>
    <button class="ghost" onclick={() => goTo(0)} aria-label="Tüm ülke" title="Tüm ülke (0)">⤢</button>
  </div>

  {#if (hovered || hoveredRoad) && !drag}
    <!-- Near the right edge (or the drawer) the tooltip opens to the left of the pointer. -->
    <div class="tooltip" class:flip={pointer.x + 330 > width - covered} style:left="{pointer.x + 14}px" style:top="{pointer.y + 14}px">
      {#if hoveredRoad}
        {@const from = nodes.get(hoveredRoad.from)}
        {@const to = nodes.get(hoveredRoad.to)}
        <strong>{from?.name} → {to?.name}</strong>
        <div>{hoveredRoad.main} kullanım{hoveredRoad.test ? ` · +${hoveredRoad.test} testte` : ""}</div>
        {#if hoveredRoad.violation}<div class="bad">Kaçak geçiş: paket export edilmemiş</div>{/if}
        {#if hoveredRoad.inCycle}<div class="bad">Döngünün parçası</div>{/if}
        <div class="muted">Tıkla: hangi satırlar?</div>
      {:else if hovered}
        <strong>{hovered.name}</strong>
        {#if hovered.kind === "module" || hovered.kind === "package"}
          <div>{count(hovered.size)} satır{hovered.jpms ? ` · JPMS: ${hovered.jpms}` : ""}</div>
          {#if hovered.metrics}
            <div>Ca {hovered.metrics.ca} · Ce {hovered.metrics.ce} · I {instability(hovered.metrics.instability)}</div>
          {/if}
          {#if hovered.exported === false}<div>🔒 Modülü bu paketi export etmiyor</div>{/if}
          {#if hovered.inCycle}<div class="bad">{hovered.kind === "module" ? "Paket döngüsü içeriyor" : "Bir döngünün parçası"}</div>{/if}
          <div class="muted">Tıkla: ayrıntılar · çift tıkla: yaklaş · <span class="out">mavi</span> giden, <span class="in">turuncu</span> gelen yollar</div>
        {:else if hovered.kind === "type"}
          <div>{hovered.typeKind ? typeText[hovered.typeKind] : ""} · {count(hovered.size)} satır{hovered.isTest ? " · test" : ""}</div>
          <div class="muted">{hovered.fqn}</div>
        {:else if hovered.kind === "library"}
          <div class="muted">{hovered.fqn}:{hovered.version ?? "?"}</div>
          <div>{hovered.matchedBy ? matchText[hovered.matchedBy] : "Kodda kullanılmıyor"}</div>
        {:else if hovered.kind === "unknown"}
          <div>Hiçbir beyan edilmiş kütüphane bu paketi açıklamıyor</div>
        {:else if hovered.kind === "jdk"}
          <div class="muted">JDK paketi</div>
        {/if}
      {/if}
    </div>
  {/if}
</div>

<style>
  .atlas { position: relative; height: 100%; min-height: 0; overflow: hidden; }
  svg { display: block; width: 100%; height: 100%; cursor: grab; touch-action: none; outline: none; user-select: none; -webkit-user-select: none; }
  svg.dragging { cursor: grabbing; }

  /* Every stroke keeps its pixel width at any zoom. */
  svg :global(rect), svg :global(ellipse), svg :global(line), svg :global(path), svg :global(polygon) { vector-effect: non-scaling-stroke; }

  .sea { fill: var(--sea); }
  .land { fill: var(--land); stroke: var(--land-edge); stroke-width: 1.5; }
  .mainland { fill: var(--land); stroke: var(--land-edge); stroke-width: 1.5; opacity: 0.85; }
  .coast-name, .archipelago-name { fill: var(--muted); font-weight: 600; letter-spacing: 0.04em; }
  .port rect { fill: var(--panel); stroke: var(--land-edge); }
  .port text { fill: var(--ink); }

  /* Regions */
  .region .fill { fill: var(--fill); }
  .region.context { opacity: 0.45; }
  .city-outline { fill: color-mix(in srgb, var(--panel) 38%, transparent); stroke: color-mix(in srgb, var(--ink) 14%, transparent); stroke-width: 0.75; }
  .city-outline.test { fill: url(#hatch); }
  .hatch-line { stroke: color-mix(in srgb, var(--ink) 30%, transparent); stroke-width: 1.2; }
  .border { fill: none; }
  .border.soft { stroke: var(--border-ink); stroke-width: 1; stroke-dasharray: 5 3; }
  .border.jpms-outer { stroke: var(--border-ink); stroke-width: 3; }
  .border.jpms-inner { stroke: var(--border-ink); stroke-width: 1; }
  .city-name { fill: var(--muted); pointer-events: none; }
  .region-name { fill: var(--ink); font-weight: 600; paint-order: stroke; stroke: var(--fill); stroke-width: 3px; pointer-events: none; }
  .region.hovered:not(.context) .border { stroke: var(--accent); }
  .region:focus, .city:focus { outline: none; }
  .region:focus-visible .border { stroke: var(--accent); stroke-width: 3; }

  /* Cities (region scale) */
  .city-fill { fill: color-mix(in srgb, var(--panel) 55%, transparent); stroke: color-mix(in srgb, var(--ink) 30%, transparent); stroke-width: 1; }
  .city.test .city-fill { fill: url(#hatch); }
  .city.cycle .city-fill { stroke: var(--bad); stroke-width: 1.5; }
  .city.hovered .city-fill, .city:focus-visible .city-fill { stroke: var(--accent); stroke-width: 2; }
  .city-fill.focus { stroke: var(--accent); stroke-width: 1.5; }
  .city-label { fill: var(--ink); font-weight: 600; pointer-events: none; }
  .lock { pointer-events: none; }
  .building-outline { fill: color-mix(in srgb, var(--ink) 9%, transparent); stroke: none; }

  /* Buildings (city scale) */
  .building rect, .building polygon { fill: color-mix(in srgb, var(--ink) 16%, var(--land)); stroke: color-mix(in srgb, var(--ink) 45%, transparent); stroke-width: 1; }
  .building.interface rect { fill: var(--panel); stroke-dasharray: 3 2; }
  .building.test rect, .building.test polygon { fill: url(#hatch); }
  .building.hovered rect, .building.hovered polygon { stroke: var(--accent); stroke-width: 2; }
  .building text { fill: var(--ink); pointer-events: none; }

  /* Islands */
  .archipelago { fill: none; stroke: color-mix(in srgb, var(--muted) 45%, transparent); stroke-width: 1; stroke-dasharray: 2 4; }
  .island ellipse { fill: var(--land); stroke: var(--land-edge); stroke-width: 1.5; }
  .island.unknown ellipse { stroke-dasharray: 4 3; fill: color-mix(in srgb, var(--land) 55%, var(--sea)); }
  .island.unused, .island.context { opacity: 0.45; }
  .island.hovered ellipse { stroke: var(--accent); stroke-width: 2; }
  .island-name { fill: var(--ink); pointer-events: none; }

  /* Roads */
  .road path { fill: none; }
  .road .casing { stroke: var(--land); opacity: 0.9; }
  .road .line { stroke: var(--road); stroke-linecap: round; opacity: 0.85; }
  .road .arrow { fill: var(--road); stroke: none; }
  .road.ferry .line { stroke: var(--ferry); stroke-dasharray: 6 4; }
  .road.ferry .arrow { fill: var(--ferry); }
  .road.coast .line { stroke: var(--muted); stroke-dasharray: 2 4; opacity: 0.6; }
  .road.coast .arrow { fill: var(--muted); }
  .road.test-only { opacity: 0.45; }
  .road.cycle .line { stroke: var(--bad); }
  .road.cycle .arrow { fill: var(--bad); }
  .road.violation .line { stroke: var(--bad); stroke-dasharray: 7 4; }
  .road.violation .arrow { fill: var(--bad); }
  .road.out .line { stroke: var(--out); opacity: 1; }
  .road.out .arrow { fill: var(--out); }
  .road.in .line { stroke: var(--in); opacity: 1; }
  .road.in .arrow { fill: var(--in); }
  .road.faded { opacity: 0.12; }
  .road.dim { opacity: 0.3; }
  .road.selected .line { stroke: var(--accent); opacity: 1; }
  .road.selected .arrow { fill: var(--accent); }
  .road { cursor: pointer; }
  .road:focus, .building:focus, .island:focus { outline: none; }

  /* Selection: an accent ring that survives hover elsewhere. */
  .region.selected .border { stroke: var(--accent); stroke-width: 3.5; stroke-dasharray: none; }
  .city-outline.selected { stroke: var(--accent); stroke-width: 2.5; }
  .city.selected .city-fill { stroke: var(--accent); stroke-width: 3; }
  .building-outline.selected { fill: var(--accent); opacity: 0.7; }
  .building.selected rect, .building.selected polygon { stroke: var(--accent); stroke-width: 3; fill: color-mix(in srgb, var(--accent) 22%, var(--land)); }
  .island.selected ellipse, .port.selected rect { stroke: var(--accent); stroke-width: 3; }
  .island.selected { opacity: 1; }
  .building, .island, .port { cursor: pointer; }

  /* Many roads at once (a big region): thinner ink, so the tiles stay readable. */
  svg.dense .road:not(.out):not(.in) .line { opacity: 0.4; }
  svg.dense .road .casing { display: none; }
  .road .hit { stroke: transparent; stroke-width: 12px; pointer-events: stroke; }

  .controls { position: absolute; right: calc(var(--covered, 0px) + 14px); top: 14px; display: flex; flex-direction: column; gap: 4px; }
  .controls button { width: 32px; height: 32px; padding: 0; background: var(--panel); font-size: 16px; }

  .tooltip {
    position: absolute;
    pointer-events: none;
    max-width: 300px;
    padding: 8px 10px;
    background: var(--panel);
    border: 1px solid var(--border);
    border-radius: 8px;
    box-shadow: 0 6px 18px rgb(0 0 0 / 0.16);
    font-size: 12px;
    line-height: 1.45;
    z-index: 5;
    overflow-wrap: anywhere;
  }
  .tooltip.flip { transform: translateX(calc(-100% - 28px)); }
  .muted { color: var(--muted); }
  .bad { color: var(--bad); }
  .out { color: var(--out); font-weight: 600; }
  .in { color: var(--in); font-weight: 600; }
</style>
