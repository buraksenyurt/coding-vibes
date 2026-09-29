<script lang="ts">
  import type { Snippet } from "svelte";
  import { repo } from "./repo.svelte";
  import { view } from "./view.svelte";

  let { children }: { children?: Snippet } = $props();

  let menuOpen = $state(false);

  const headLabel = $derived.by(() => {
    const head = repo.summary?.head;
    if (!head) return "";
    switch (head.kind) {
      case "branch": return head.name;
      case "detached": return `detached @ ${head.target.slice(0, 7)}`;
      case "unborn": return `${head.name} (boş)`;
    }
  });

  async function openRecent(path: string) {
    menuOpen = false;
    await repo.open(path);
  }
</script>

<header class="toolbar">
  <div class="picker">
    <button class="primary" onclick={() => repo.pickAndOpen()} disabled={repo.loading}>Repo seç</button>
    <button class="icon" aria-label="Son açılanlar" onclick={() => (menuOpen = !menuOpen)} disabled={repo.recent.length === 0}>▾</button>
    {#if menuOpen}
      <ul class="menu" role="menu">
        {#each repo.recent as item (item.path)}
          <li><button role="menuitem" onclick={() => openRecent(item.path)} title={item.path}>
            <strong>{item.name}</strong><span>{item.path}</span>
          </button></li>
        {/each}
      </ul>
    {/if}
  </div>

  {#if repo.summary}
    <div class="repo" title={repo.summary.path}>
      <strong>{repo.summary.name}</strong>
      <span class="head">HEAD: {headLabel}</span>
    </div>
    <button class="icon" aria-label="Yenile" title="Yenile" onclick={() => repo.refresh()} disabled={repo.loading}>⟳</button>
  {/if}

  <div class="spacer"></div>

  {#if repo.map}
    <div class="segmented" role="group" aria-label="Geçiş renkleri">
      <span class="caption">Geçiş rengi</span>
      <button class:active={view.colorMode === "kind"} aria-pressed={view.colorMode === "kind"}
              onclick={() => view.setColorMode("kind")}>Türe göre</button>
      <button class:active={view.colorMode === "lane"} aria-pressed={view.colorMode === "lane"}
              onclick={() => view.setColorMode("lane")}>Dala göre</button>
    </div>
    <label class="toggle">
      <input type="checkbox" checked={view.compact} onchange={() => view.toggleCompact()} />
      Kompakt
    </label>
  {/if}
  {@render children?.()}
</header>

<style>
  .toolbar {
    display: flex;
    align-items: center;
    gap: 12px;
    padding: 8px 12px;
    border-bottom: 1px solid var(--border);
    background: var(--surface);
  }
  .spacer { flex: 1; }
  .picker { position: relative; display: flex; }
  button {
    font: inherit;
    color: var(--text);
    background: transparent;
    border: 1px solid var(--border);
    border-radius: 6px;
    padding: 5px 10px;
    cursor: pointer;
  }
  button:disabled { opacity: 0.5; cursor: default; }
  button.primary { background: var(--accent); border-color: var(--accent); color: white; border-radius: 6px 0 0 6px; }
  .picker .icon { border-radius: 0 6px 6px 0; border-left: none; }
  .segmented { display: flex; align-items: center; }
  .caption { color: var(--muted); font-size: 12px; margin-right: 8px; }
  .segmented button { border-radius: 0; font-size: 12px; padding: 4px 10px; }
  .segmented button:first-of-type { border-radius: 6px 0 0 6px; }
  .segmented button:last-of-type { border-radius: 0 6px 6px 0; border-left: none; }
  .segmented button.active { background: var(--text); color: var(--surface); border-color: var(--text); }
  .toggle { display: flex; align-items: center; gap: 6px; font-size: 12px; color: var(--text-secondary); cursor: pointer; }
  .repo { display: flex; gap: 10px; align-items: baseline; }
  .head { color: var(--muted); font-size: 12px; }
  .menu {
    position: absolute;
    top: calc(100% + 4px);
    left: 0;
    z-index: 10;
    min-width: 320px;
    margin: 0;
    padding: 4px;
    list-style: none;
    background: var(--surface);
    border: 1px solid var(--border);
    border-radius: 8px;
    box-shadow: 0 8px 24px rgb(0 0 0 / 0.2);
  }
  .menu button {
    width: 100%;
    border: none;
    text-align: left;
    display: flex;
    flex-direction: column;
    border-radius: 4px;
  }
  .menu button:hover { background: var(--bg); }
  .menu span { color: var(--muted); font-size: 12px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
</style>
