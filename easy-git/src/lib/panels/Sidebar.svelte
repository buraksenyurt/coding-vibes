<script lang="ts">
  import type { BranchStatsDto } from "../bindings/BranchStatsDto";
  import { ago } from "../format";
  import { laneColor } from "../metro/geometry";
  import { repo } from "../repo.svelte";

  let { width = 260 }: { width?: number } = $props();

  interface Group {
    folder: string | null;
    branches: BranchStatsDto[];
  }

  // `feature/login` and `feature/payments` fold under a `feature/` folder.
  const groups = $derived.by<Group[]>(() => {
    const top: BranchStatsDto[] = [];
    const folders = new Map<string, BranchStatsDto[]>();
    const sorted = [...repo.stats].sort((a, b) => Number(a.isRemoteOnly) - Number(b.isRemoteOnly) || a.name.localeCompare(b.name));
    for (const s of sorted) {
      const slash = s.isRemoteOnly ? -1 : s.name.indexOf("/");
      if (slash < 0) top.push(s);
      else {
        const folder = s.name.slice(0, slash + 1);
        folders.set(folder, [...(folders.get(folder) ?? []), s]);
      }
    }
    // Trunk-like names first, in the same spirit as the lane priority.
    const order = ["main", "master", "trunk", "develop", "dev"];
    top.sort((a, b) => {
      const ra = order.indexOf(a.name), rb = order.indexOf(b.name);
      return (ra < 0 ? 99 : ra) - (rb < 0 ? 99 : rb) || Number(a.isRemoteOnly) - Number(b.isRemoteOnly);
    });
    return [
      { folder: null, branches: top.filter((s) => !s.isRemoteOnly) },
      ...[...folders.entries()].map(([folder, branches]) => ({ folder, branches })),
      { folder: "yalnızca remote", branches: top.filter((s) => s.isRemoteOnly) },
    ].filter((g) => g.branches.length > 0);
  });

  let collapsed = $state<Set<string>>(new Set());
  function toggleFolder(folder: string) {
    const next = new Set(collapsed);
    if (next.has(folder)) next.delete(folder);
    else next.add(folder);
    collapsed = next;
  }

  const colorOf = (s: BranchStatsDto) => (s.lane === null || !repo.map ? null : laneColor(repo.map.lanes[s.lane].color));
  const shortName = (s: BranchStatsDto, folder: string | null) =>
    folder && !s.isRemoteOnly && s.name.startsWith(folder) ? s.name.slice(folder.length) : s.name;
  const selected = (s: BranchStatsDto) => repo.selection?.kind === "branch" && repo.selection.name === s.name;
</script>

<nav class="sidebar" style:width="{width}px" aria-label="Dallar">
  <h2>Dallar <span class="count">{repo.stats.length}</span></h2>
  {#each groups as group (group.folder ?? "")}
    {#if group.folder}
      <button class="folder" onclick={() => toggleFolder(group.folder!)} aria-expanded={!collapsed.has(group.folder)}>
        <span class="chevron">{collapsed.has(group.folder) ? "▸" : "▾"}</span>{group.folder}
      </button>
    {/if}
    {#if !group.folder || !collapsed.has(group.folder)}
      <ul class:nested={group.folder !== null}>
        {#each group.branches as s (s.name)}
          {@const color = colorOf(s)}
          {@const hidden = s.lane !== null && repo.hiddenLanes.has(s.lane)}
          <li class:selected={selected(s)} class:hidden>
            <button class="branch" onclick={() => repo.selectBranch(s.name)} title="{s.name} — son commit {ago(s.lastActivity)}">
              {#if color}
                <span class="swatch" style:background={color}></span>
              {:else}
                <span class="swatch hollow" title="Kendi commit'i yok; başka bir hattın istasyonunda duruyor"></span>
              {/if}
              <span class="name">{shortName(s, group.folder)}</span>
              <span class="badges">
                {#if s.stale}<span class="badge stale" title="30 günden uzun süredir commit yok ve merge edilmemiş">bayat</span>{/if}
                {#if s.merged}<span class="badge merged" title="Ana dala merge edilmiş">✓</span>{/if}
                {#if s.ahead > 0 || s.behind > 0}
                  <span class="badge counts" title="Ana dala göre: {s.ahead} önde, {s.behind} geride">
                    {#if s.ahead > 0}↑{s.ahead}{/if}{#if s.behind > 0} ↓{s.behind}{/if}
                  </span>
                {/if}
                {#if s.upstream}
                  <span class="badge remote" class:diverged={s.upstream.ahead + s.upstream.behind > 0}
                        title="{s.upstream.name}: {s.upstream.ahead} önde, {s.upstream.behind} geride">
                    ☁{#if s.upstream.ahead > 0} ↑{s.upstream.ahead}{/if}{#if s.upstream.behind > 0} ↓{s.upstream.behind}{/if}
                  </span>
                {/if}
              </span>
            </button>
            {#if s.lane !== null}
              <button class="eye" onclick={() => repo.toggleLane(s.lane!)}
                      aria-label={hidden ? "Hattı göster" : "Hattı gizle"} title={hidden ? "Hattı göster" : "Hattı gizle"}>
                {hidden ? "◌" : "●"}
              </button>
            {/if}
          </li>
        {/each}
      </ul>
    {/if}
  {/each}
</nav>

<style>
  .sidebar {
    flex: none;
    overflow-y: auto;
    padding: 8px 0 16px;
    background: var(--surface);
    border-right: 1px solid var(--border);
    font-size: 13px;
  }
  h2 {
    margin: 4px 14px 8px;
    font-size: 11px;
    font-weight: 600;
    text-transform: uppercase;
    letter-spacing: 0.06em;
    color: var(--muted);
  }
  .count { font-weight: 400; }
  ul { list-style: none; margin: 0; padding: 0; }
  ul.nested li .branch { padding-left: 30px; }
  button { font: inherit; color: inherit; background: none; border: none; cursor: pointer; }
  .folder {
    display: flex;
    align-items: center;
    gap: 4px;
    width: 100%;
    padding: 5px 14px;
    color: var(--text-secondary);
    text-align: left;
  }
  .chevron { width: 12px; font-size: 10px; color: var(--muted); }
  li { display: flex; align-items: center; }
  li:hover, li.selected { background: var(--bg); }
  li.selected { box-shadow: inset 3px 0 0 var(--accent); }
  li.hidden .name, li.hidden .swatch { opacity: 0.4; }
  .branch {
    flex: 1;
    min-width: 0;
    display: flex;
    align-items: center;
    gap: 8px;
    padding: 5px 4px 5px 14px;
    text-align: left;
  }
  .name { flex: 1; min-width: 0; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  .swatch { flex: none; width: 14px; height: 4px; border-radius: 2px; }
  .swatch.hollow { height: 8px; width: 8px; margin: 0 3px; border-radius: 50%; border: 1.5px solid var(--muted); }
  .badges { display: flex; gap: 4px; flex: none; }
  .badge {
    font-size: 11px;
    line-height: 16px;
    padding: 0 5px;
    border-radius: 8px;
    color: var(--text-secondary);
    background: var(--bg);
    border: 1px solid var(--border);
    font-variant-numeric: tabular-nums;
    white-space: nowrap;
  }
  .badge.merged { color: var(--text-secondary); }
  .badge.stale { border-color: currentColor; color: #a06a00; }
  :global(:root[data-theme="dark"]) .badge.stale { color: #fab219; }
  @media (prefers-color-scheme: dark) { :global(:root:not([data-theme="light"])) .badge.stale { color: #fab219; } }
  .badge.remote.diverged { color: var(--text); }
  .eye { flex: none; width: 28px; color: var(--muted); font-size: 11px; opacity: 0; }
  li:hover .eye, li.hidden .eye, .eye:focus-visible { opacity: 1; }
</style>
