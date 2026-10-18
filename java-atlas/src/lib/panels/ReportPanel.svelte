<script lang="ts">
  import { project } from "../project.svelte";
  import { count, duration } from "../format";
  import type { FindingDto } from "../bindings/FindingDto";

  const s = $derived(project.summary!);

  const findingText: Record<FindingDto["kind"], string> = {
    unusedModule: "Beyan edilmiş, kullanılmıyor",
    unusedLibrary: "Beyan edilmiş, kullanılmıyor",
    undeclaredModule: "Kullanılıyor, beyan edilmemiş",
    undeclaredLibrary: "Kullanılıyor, beyan edilmemiş",
  };

  function findingNote(f: FindingDto): string {
    if (f.kind === "undeclaredModule") return f.reachable ? "dolaylı yoldan geliyor" : "erişilemiyor";
    return "";
  }

  const tiles = $derived([
    ["Modül", s.modules],
    ["Paket", s.packages],
    ["Tip", s.types],
    ["Dosya", s.files],
    ["Kütüphane", s.libraries],
    ["Bilinmeyen paket", s.unknownPackages],
  ] as const);
</script>

<aside class="report">
  <div class="tiles">
    {#each tiles as [label, value] (label)}
      <div class="tile">
        <div class="value">{count(value)}</div>
        <div class="label">{label}</div>
      </div>
    {/each}
  </div>
  <p class="timing">
    Tarama {duration(s.scanMs)} · analiz {duration(s.analysisMs)} · {s.indexedJars} jar indekslendi
  </p>

  <section>
    <h2>Döngüler <span class="badge" class:bad={s.cycles.length > 0}>{s.cycles.length}</span></h2>
    {#each s.cycles as c, i (i)}
      <div class="item">
        <div class="mono links">
          ⟲
          {#each c.packages as p, k (p.key)}
            {#if k > 0}<span class="sep">⇄</span>{/if}
            <button class="inline" onclick={() => project.goToNode(p.key)}>{p.name}</button>
          {/each}
        </div>
        <div class="hint">
          En zayıf halka: <span class="mono">{c.weakestFrom.name} → {c.weakestTo.name}</span> ({c.weakestWeight} kullanım)
        </div>
      </div>
    {:else}
      <p class="none">Döngü yok.</p>
    {/each}
  </section>

  <section>
    <h2>JPMS ihlalleri <span class="badge" class:bad={s.violations.length > 0}>{s.violations.length}</span></h2>
    {#each s.violations as v, i (i)}
      <div class="item">
        <div class="mono">
          ✗ <button class="inline" onclick={() => project.goToNode(v.fromModule.key)}>{v.fromModule.name}</button>
          → <button class="inline" onclick={() => project.goToNode(v.toPackage.key)}>{v.toPackage.name}</button>
        </div>
        <div class="hint">
          {v.kind === "notExported" ? "Paket export edilmiyor" : "Modül requires edilmemiş"}
        </div>
        <ul class="evidence">
          {#each v.evidence as e (e.file + e.line)}
            <li class="mono">{e.file}:{e.line}</li>
          {/each}
        </ul>
      </div>
    {:else}
      <p class="none">İhlal yok.</p>
    {/each}
  </section>

  <section>
    <h2><span><span lang="en">Build</span> dosyası ↔ kod</span> <span class="badge" class:warn={s.findings.length > 0}>{s.findings.length}</span></h2>
    {#each s.findings as f, i (i)}
      <div class="item">
        <div class="mono">
          <button class="inline" onclick={() => project.goToNode(f.module.key)}>{f.module.name}</button>
          → <button class="inline" onclick={() => project.goToNode(f.target.key)}>{f.target.name}</button>
        </div>
        <div class="hint">{findingText[f.kind]} {findingNote(f) ? `(${findingNote(f)})` : ""}</div>
      </div>
    {:else}
      <p class="none">Build dosyaları kodla uyumlu.</p>
    {/each}
  </section>

  {#if s.warnings.length > 0}
    <section>
      <h2>Okuma uyarıları <span class="badge warn">{s.warnings.length}</span></h2>
      {#each s.warnings as w, i (i)}
        <div class="item">
          <div class="mono small">{w.file}</div>
          <div class="hint">{w.message}</div>
        </div>
      {/each}
    </section>
  {/if}
</aside>

<style>
  .report { overflow: auto; padding: 14px; background: var(--panel); }
  /* Every name is a link: it flies the map there and opens its details. */
  .inline { background: none; border: none; padding: 0; color: var(--accent); font: inherit; cursor: pointer; text-align: left; overflow-wrap: anywhere; }
  .inline:hover { text-decoration: underline; }
  .links { display: flex; flex-wrap: wrap; gap: 4px; align-items: baseline; }
  .sep { color: var(--muted); }
  .tiles { display: grid; grid-template-columns: repeat(3, 1fr); gap: 8px; }
  .tile { padding: 10px; border: 1px solid var(--border); border-radius: 8px; }
  .value { font-size: 20px; font-weight: 600; font-variant-numeric: tabular-nums; }
  .label { color: var(--muted); font-size: 12px; }
  .timing { color: var(--muted); font-size: 12px; margin: 8px 0 4px; }
  section { margin-top: 18px; }
  /* The document is lang="tr", so `uppercase` turns "Build" into "BUİLD";
     English words are wrapped in lang="en" to keep their dotless I. */
  h2 { font-size: 13px; text-transform: uppercase; letter-spacing: 0.04em; color: var(--muted); margin: 0 0 8px; display: flex; gap: 8px; align-items: center; }
  .badge { font-size: 11px; padding: 1px 7px; border-radius: 10px; background: var(--hover); color: var(--ink); }
  .badge.bad { background: var(--bad); color: white; }
  .badge.warn { background: var(--warn); color: #222; }
  .item { padding: 8px 10px; border: 1px solid var(--border); border-radius: 8px; margin-bottom: 6px; }
  .mono { font-family: ui-monospace, "Cascadia Code", "JetBrains Mono", Menlo, monospace; font-size: 12px; overflow-wrap: anywhere; }
  .small { font-size: 11px; }
  .hint { color: var(--muted); font-size: 12px; margin-top: 3px; }
  .none { color: var(--muted); font-size: 13px; margin: 0; }
  .evidence { margin: 6px 0 0; padding-left: 16px; color: var(--muted); }
</style>
