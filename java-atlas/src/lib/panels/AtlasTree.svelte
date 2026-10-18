<script lang="ts">
  // Modules and their packages, then the islands: the map as a list.
  // Clicking an entry flies the map to it and opens its details.
  import { untrack } from "svelte";
  import { project } from "../project.svelte";
  import { count } from "../format";

  let open = $state<Record<string, boolean>>({});

  const tree = $derived(project.tree);
  const selectedKey = $derived(project.selection?.kind === "node" ? project.selection.details.node.key : null);
  /** Libraries used in code but not declared by some module. */
  const undeclared = $derived(
    new Set((project.summary?.findings ?? []).filter((f) => f.kind === "undeclaredLibrary").map((f) => f.target.key)),
  );

  function toggle(key: string) {
    open = { ...open, [key]: !open[key] };
  }

  // Opening a node from anywhere (search, map) unfolds its module here.
  $effect(() => {
    const path = project.selection?.kind === "node" ? project.selection.details.path : [];
    const module = path[0];
    // Only the selection is tracked: collapsing the module by hand must stay collapsed.
    if (module?.kind === "module") untrack(() => !open[module.key] && (open = { ...open, [module.key]: true }));
  });
</script>

<nav class="tree" aria-label="Atlas ağacı">
  {#if tree}
    <h2>Bölgeler</h2>
    <ul role="tree">
      {#each tree.modules as m (m.module.key)}
        {@const expanded = !!open[m.module.key]}
        <li role="treeitem" aria-expanded={expanded} aria-selected={selectedKey === m.module.key}>
          <div class="row" class:selected={selectedKey === m.module.key}>
            <button class="twisty" onclick={() => toggle(m.module.key)} aria-label={expanded ? "Daralt" : "Genişlet"}>{expanded ? "▾" : "▸"}</button>
            <button class="name" onclick={() => project.goToNode(m.module.key)}>
              <span>{m.module.name}</span>
              <span class="badges">
                {#if m.module.jpms}<span title="JPMS modülü">▣</span>{/if}
                {#if m.module.inCycle}<span class="bad" title="Paket döngüsü içeriyor">⟲</span>{/if}
                <span class="muted">{count(m.module.size)}</span>
              </span>
            </button>
          </div>
          {#if expanded}
            <ul role="group">
              {#each m.packages as p (p.key)}
                <li role="treeitem" aria-selected={selectedKey === p.key}>
                  <div class="row child" class:selected={selectedKey === p.key} class:test={p.isTest}>
                    <button class="name" onclick={() => project.goToNode(p.key)} title={p.name}>
                      <span class="pkg">{p.name}</span>
                      <span class="badges">
                        {#if p.exported === false}<span title="Export edilmemiş">🔒</span>{/if}
                        {#if p.inCycle}<span class="bad" title="Döngünün parçası">⟲</span>{/if}
                        {#if p.isTest}<span class="muted" title="Sadece test kodu">test</span>{/if}
                      </span>
                    </button>
                  </div>
                </li>
              {/each}
            </ul>
          {/if}
        </li>
      {/each}
    </ul>

    <h2>Adalar</h2>
    <ul role="tree">
      {#each tree.islands as i (i.key)}
        <li role="treeitem" aria-selected={selectedKey === i.key}>
          <div class="row" class:selected={selectedKey === i.key}>
            <span class="twisty" aria-hidden="true">{i.kind === "unknown" ? "?" : "◌"}</span>
            <button class="name" onclick={() => project.goToNode(i.key)} title={i.fqn}>
              <span class:muted={i.kind === "library" && !i.matchedBy}>{i.name}</span>
              <span class="badges">
                {#if i.kind === "library" && !i.matchedBy}<span class="muted" title="Kodda kullanılmıyor">✕</span>{/if}
                {#if undeclared.has(i.key)}<span class="warn" title="Bir modül kullanıyor ama beyan etmemiş">!</span>{/if}
                {#if i.matchedBy === "jar"}<span class="muted" title="Paketler jar'dan doğrulandı">jar</span>{/if}
              </span>
            </button>
          </div>
        </li>
      {/each}
    </ul>
  {/if}
</nav>

<style>
  .tree { overflow: auto; padding: 8px 8px 16px; font-size: 13px; }
  h2 { font-size: 12px; text-transform: uppercase; letter-spacing: 0.04em; color: var(--muted); margin: 10px 6px 4px; }
  ul { list-style: none; margin: 0; padding: 0; }
  ul ul { padding-left: 18px; }
  .row { display: flex; align-items: center; border-radius: 6px; }
  .row.selected { background: color-mix(in srgb, var(--accent) 16%, transparent); }
  .row:hover:not(.selected) { background: var(--hover); }
  .twisty {
    width: 22px;
    flex-shrink: 0;
    text-align: center;
    background: none;
    border: none;
    color: var(--muted);
    cursor: pointer;
    font: inherit;
    padding: 4px 0;
  }
  span.twisty { cursor: default; }
  .name {
    flex: 1;
    min-width: 0;
    display: flex;
    justify-content: space-between;
    gap: 8px;
    padding: 4px 6px;
    background: none;
    border: none;
    color: var(--ink);
    font: inherit;
    text-align: left;
    cursor: pointer;
  }
  .name > span:first-child { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  .pkg { font-family: ui-monospace, "Cascadia Code", Menlo, monospace; font-size: 12px; }
  .badges { display: flex; gap: 6px; flex-shrink: 0; font-size: 12px; }
  .muted { color: var(--muted); }
  .bad { color: var(--bad); }
  .warn { color: var(--warn); font-weight: 700; }
  .child.test .pkg { color: var(--muted); }
</style>
