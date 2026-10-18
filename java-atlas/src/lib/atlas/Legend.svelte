<script lang="ts">
  let open = $state(true);
</script>

<aside class="legend" class:closed={!open} aria-label="Lejant">
  <button class="toggle" onclick={() => (open = !open)} aria-expanded={open}>{open ? "Lejant ▾" : "Lejant ▸"}</button>
  {#if open}
    <ul>
      <li><svg width="34" height="10" aria-hidden="true"><line x1="2" y1="5" x2="32" y2="5" class="road" /></svg> Bağımlılık (kalınlık = kullanım)</li>
      <li><svg width="34" height="10" aria-hidden="true"><line x1="2" y1="5" x2="32" y2="5" class="ferry" /></svg> Feribot: dış kütüphane</li>
      <li><svg width="34" height="10" aria-hidden="true"><line x1="2" y1="5" x2="32" y2="5" class="cycle" /></svg> Döngü</li>
      <li><svg width="34" height="10" aria-hidden="true"><line x1="2" y1="5" x2="32" y2="5" class="violation" /></svg> Kaçak geçiş (JPMS)</li>
      <li><svg width="34" height="10" aria-hidden="true"><line x1="2" y1="5" x2="32" y2="5" class="out" /></svg> Giden · <svg width="20" height="10" aria-hidden="true"><line x1="2" y1="5" x2="18" y2="5" class="in" /></svg> Gelen</li>
      <li><svg width="34" height="14" aria-hidden="true"><rect x="2" y="2" width="30" height="10" rx="2" class="jpms" /><rect x="4.5" y="4.5" width="25" height="5" rx="1" class="jpms-in" /></svg> Gümrüklü sınır (JPMS)</li>
      <li><svg width="34" height="14" aria-hidden="true"><rect x="2" y="2" width="30" height="10" rx="2" class="soft" /></svg> Yumuşak sınır</li>
      <li><svg width="34" height="14" aria-hidden="true"><ellipse cx="17" cy="7" rx="12" ry="5" class="island unused" /></svg> Kullanılmayan kütüphane</li>
      <li><span class="glyph">⟲</span> Döngü içeriyor · <span class="glyph">🔒</span> export edilmemiş</li>
    </ul>
  {/if}
</aside>

<style>
  .legend {
    position: absolute;
    right: 14px;
    bottom: 14px;
    padding: 6px 10px 8px;
    background: var(--panel);
    border: 1px solid var(--border);
    border-radius: 8px;
    box-shadow: 0 4px 14px rgb(0 0 0 / 0.1);
    font-size: 11px;
    z-index: 4;
  }
  .legend.closed { padding-bottom: 6px; }
  .toggle { background: none; border: none; padding: 0; color: var(--muted); font: inherit; font-weight: 600; cursor: pointer; }
  ul { list-style: none; margin: 4px 0 0; padding: 0; display: grid; gap: 2px; }
  li { display: flex; align-items: center; gap: 8px; color: var(--ink); }
  svg { flex-shrink: 0; }
  line { stroke-width: 3; stroke-linecap: round; }
  .road { stroke: var(--road); }
  .ferry { stroke: var(--ferry); stroke-dasharray: 5 3; stroke-width: 2; }
  .cycle { stroke: var(--bad); }
  .violation { stroke: var(--bad); stroke-dasharray: 6 3; }
  .out { stroke: var(--out); }
  .in { stroke: var(--in); }
  rect { fill: var(--land); }
  .jpms { stroke: var(--border-ink); stroke-width: 2.5; }
  .jpms-in { fill: none; stroke: var(--border-ink); stroke-width: 0.8; }
  .soft { stroke: var(--border-ink); stroke-width: 1; stroke-dasharray: 4 2; }
  .island { fill: var(--land); stroke: var(--land-edge); stroke-width: 1.2; }
  .unused { opacity: 0.45; }
  .glyph { width: 34px; text-align: center; }
</style>
