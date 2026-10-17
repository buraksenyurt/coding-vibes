<script lang="ts">
  // Phase 4 preview of the country scale as plain lists. Phase 5 replaces
  // this with the drawn map; the data is already the one the map will use.
  import { project } from "../project.svelte";
  import { count, instability } from "../format";
  import type { NodeDto } from "../bindings/NodeDto";

  const view = $derived(project.country);
  const byKey = $derived(new Map([...(view?.members ?? []), ...(view?.neighbours ?? [])].map((n) => [n.key, n])));
  const islands = $derived((view?.neighbours ?? []).filter((n) => n.kind === "library" || n.kind === "unknown"));
  const mainland = $derived((view?.neighbours ?? []).filter((n) => n.kind === "jdk"));

  function roadsFrom(node: NodeDto) {
    return (view?.edges ?? []).filter((e) => e.from === node.key);
  }

  const matchText: Record<string, string> = { jar: "jar", name: "ad benzerliği", mixed: "karışık" };
</script>

{#if view}
  <div class="preview">
    <p class="note">Ülke ölçeği — harita Faz 5'te burada çizilecek. Şimdilik aynı veri liste olarak.</p>

    <h2>Bölgeler</h2>
    <div class="grid">
      {#each view.members as m (m.key)}
        <article class="region" class:cycle={m.inCycle}>
          <header>
            <strong>{m.name}</strong>
            {#if m.jpms}<span class="tag" title="JPMS modülü">▣ {m.jpms}</span>{/if}
            {#if m.inCycle}<span class="tag bad" title="İçinde paket döngüsü var">⟲</span>{/if}
          </header>
          <div class="meta">
            {count(m.size)} satır · Ca {m.metrics?.ca ?? 0} · Ce {m.metrics?.ce ?? 0} · I {instability(m.metrics?.instability)}
          </div>
          <ul>
            {#each roadsFrom(m) as e (e.to)}
              {@const target = byKey.get(e.to)}
              <li class:violation={e.violation}>
                <span class="to">{target?.kind === "library" ? "⇢" : "→"} {target?.name ?? e.to}</span>
                <span class="weight">{e.main}{e.test ? ` +${e.test}` : ""}{e.violation ? " ✗" : ""}</span>
              </li>
            {:else}
              <li class="none">Giden yol yok</li>
            {/each}
          </ul>
        </article>
      {/each}
    </div>

    {#if islands.length}
      <h2>Adalar</h2>
      <div class="grid">
        {#each islands as i (i.key)}
          <article class="island" class:unused={i.kind === "library" && !i.matchedBy}>
            <strong>{i.name}</strong>
            <div class="meta">
              {#if i.kind === "unknown"}
                bilinmeyen paket
              {:else}
                {i.version ?? "?"} · {i.matchedBy ? matchText[i.matchedBy] : "kullanılmıyor"}
              {/if}
            </div>
          </article>
        {/each}
      </div>
    {/if}

    {#if mainland.length}
      <h2>Anakara (JDK)</h2>
      <p class="mono">{mainland.map((j) => j.name).join(", ")}</p>
    {/if}
  </div>
{/if}

<style>
  .preview { overflow: auto; padding: 16px 20px; }
  .note { color: var(--muted); font-size: 13px; margin: 0 0 12px; }
  h2 { font-size: 13px; text-transform: uppercase; letter-spacing: 0.04em; color: var(--muted); margin: 18px 0 8px; }
  .grid { display: grid; grid-template-columns: repeat(auto-fill, minmax(240px, 1fr)); gap: 10px; }
  .region, .island { padding: 10px 12px; background: var(--land); border: 1px solid var(--land-edge); border-radius: 10px; }
  .region.cycle { border-color: var(--bad); }
  .island { background: var(--panel); border-style: dashed; overflow-wrap: anywhere; }
  .island.unused { opacity: 0.55; }
  header { display: flex; align-items: center; gap: 6px; flex-wrap: wrap; }
  .tag { font-size: 11px; padding: 1px 6px; border-radius: 8px; background: var(--panel); color: var(--muted); }
  .tag.bad { background: var(--bad); color: white; }
  .meta { color: var(--muted); font-size: 12px; margin: 4px 0 6px; font-variant-numeric: tabular-nums; }
  ul { list-style: none; margin: 0; padding: 0; font-size: 12px; }
  li { display: flex; justify-content: space-between; gap: 8px; padding: 2px 0; }
  li.violation { color: var(--bad); font-weight: 600; }
  li.none { color: var(--muted); }
  .to { min-width: 0; overflow-wrap: anywhere; }
  .weight { flex-shrink: 0; font-variant-numeric: tabular-nums; color: var(--muted); }
  li.violation .weight { color: var(--bad); }
  .mono { font-family: ui-monospace, "Cascadia Code", Menlo, monospace; font-size: 12px; color: var(--muted); }
</style>
