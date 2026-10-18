<script lang="ts">
  // Find a module, package, type or library by name and fly there.
  // "/" or Ctrl+F focuses the box from anywhere.
  import * as api from "./api";
  import type { NodeKindDto } from "./bindings/NodeKindDto";
  import type { SearchHitDto } from "./bindings/SearchHitDto";
  import { project } from "./project.svelte";

  let query = $state("");
  let hits = $state<SearchHitDto[]>([]);
  let active = $state(0);
  let open = $state(false);
  let input = $state<HTMLInputElement | null>(null);

  const kindIcon: Record<NodeKindDto, string> = { module: "▦", package: "▤", type: "▪", library: "⬭", jdk: "⚓", unknown: "?" };
  const kindText: Record<NodeKindDto, string> = {
    module: "modül",
    package: "paket",
    type: "tip",
    library: "kütüphane",
    jdk: "JDK",
    unknown: "bilinmeyen",
  };

  // Debounced: one request per pause in typing, and an answer to an older
  // query never overwrites a newer one.
  let timer: ReturnType<typeof setTimeout> | undefined;
  let asked = 0;
  function onInput() {
    clearTimeout(timer);
    const q = query.trim();
    if (!q) {
      hits = [];
      return;
    }
    timer = setTimeout(async () => {
      const ticket = ++asked;
      const result = await api.search(q).catch(() => []);
      if (ticket !== asked) return;
      hits = result;
      active = 0;
      open = true;
    }, 120);
  }

  function choose(hit: SearchHitDto) {
    open = false;
    query = hit.node.name;
    input?.blur();
    project.goToNode(hit.node.key);
  }

  function onKey(e: KeyboardEvent) {
    if (e.key === "ArrowDown" && hits.length) {
      active = (active + 1) % hits.length;
      open = true;
    } else if (e.key === "ArrowUp" && hits.length) {
      active = (active - 1 + hits.length) % hits.length;
    } else if (e.key === "Enter" && hits[active]) {
      choose(hits[active]);
    } else if (e.key === "Escape") {
      if (open && hits.length) open = false;
      else {
        query = "";
        hits = [];
        input?.blur();
      }
    } else return;
    e.preventDefault();
    e.stopPropagation();
  }

  function onWindowKey(e: KeyboardEvent) {
    const target = e.target as HTMLElement | null;
    const typing = target?.closest("input, textarea, select, [contenteditable]");
    if ((e.key === "/" && !typing) || ((e.ctrlKey || e.metaKey) && e.key.toLowerCase() === "f")) {
      e.preventDefault();
      input?.focus();
      input?.select();
    }
  }
</script>

<svelte:window onkeydown={onWindowKey} />

<div class="search" role="combobox" aria-expanded={open && hits.length > 0} aria-haspopup="listbox" aria-controls="search-hits">
  <input
    bind:this={input}
    bind:value={query}
    oninput={onInput}
    onkeydown={onKey}
    onfocus={() => (open = hits.length > 0)}
    onblur={() => setTimeout(() => (open = false), 150)}
    type="search"
    placeholder="Ara… ( / )"
    aria-label="Modül, paket, tip veya kütüphane ara"
    aria-autocomplete="list"
    spellcheck="false"
    autocomplete="off"
  />
  {#if open && query.trim()}
    <ul id="search-hits" role="listbox">
      {#each hits as hit, i (hit.node.key)}
        <li role="option" aria-selected={i === active}>
          <button class:active={i === active} onpointerdown={(e) => e.preventDefault()} onclick={() => choose(hit)} onpointerenter={() => (active = i)}>
            <span class="icon" title={kindText[hit.node.kind]}>{kindIcon[hit.node.kind]}</span>
            <span class="text">
              <span class="name">{hit.node.name}</span>
              {#if hit.context}<span class="context">{hit.context}</span>{/if}
            </span>
            <span class="kind">{kindText[hit.node.kind]}</span>
          </button>
        </li>
      {:else}
        <li class="empty">Sonuç yok</li>
      {/each}
    </ul>
  {/if}
</div>

<style>
  .search { position: relative; width: clamp(180px, 22vw, 300px); }
  input {
    width: 100%;
    box-sizing: border-box;
    padding: 5px 10px;
    border: 1px solid var(--border);
    border-radius: 6px;
    background: var(--sea);
    color: var(--ink);
    font: inherit;
    font-size: 13px;
  }
  input:focus { outline: 2px solid var(--accent); outline-offset: -1px; }
  ul {
    position: absolute;
    z-index: 25;
    top: calc(100% + 4px);
    left: 0;
    right: 0;
    min-width: 320px;
    max-height: 60vh;
    overflow: auto;
    margin: 0;
    padding: 4px;
    list-style: none;
    background: var(--panel);
    border: 1px solid var(--border);
    border-radius: 8px;
    box-shadow: 0 8px 24px rgb(0 0 0 / 0.18);
  }
  li.empty { padding: 8px; color: var(--muted); font-size: 13px; }
  button {
    display: flex;
    gap: 8px;
    align-items: center;
    width: 100%;
    padding: 5px 8px;
    background: none;
    border: none;
    border-radius: 6px;
    color: var(--ink);
    font: inherit;
    font-size: 13px;
    text-align: left;
    cursor: pointer;
  }
  button.active { background: var(--hover); }
  .icon { width: 16px; text-align: center; color: var(--muted); }
  .text { flex: 1; min-width: 0; display: flex; flex-direction: column; }
  .name, .context { overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  .context { color: var(--muted); font-size: 11px; }
  .kind { color: var(--muted); font-size: 11px; }
</style>
