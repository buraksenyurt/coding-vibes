<script lang="ts">
  import type { Crumb } from "../project.svelte";

  let { crumbs, onselect }: { crumbs: Crumb[]; onselect: (index: number) => void } = $props();

  const scaleName: Record<Crumb["scale"], string> = { country: "Ülke", region: "Bölge", city: "Şehir" };
</script>

<nav class="crumbs" aria-label="Konum">
  {#each crumbs as c, i (c.scale + (c.key ?? ""))}
    {#if i > 0}<span class="sep" aria-hidden="true">▸</span>{/if}
    {#if i < crumbs.length - 1}
      <button onclick={() => onselect(i)} title="{scaleName[c.scale]} ölçeğine dön">{c.name}</button>
    {:else}
      <span class="current" aria-current="location">
        {c.name}
        {#if c.scale !== "country"}<span class="scale">{scaleName[c.scale]}</span>{/if}
      </span>
    {/if}
  {/each}
</nav>

<style>
  .crumbs {
    position: absolute;
    left: 14px;
    top: 14px;
    display: flex;
    align-items: center;
    gap: 6px;
    max-width: calc(100% - 80px);
    padding: 6px 10px;
    background: var(--panel);
    border: 1px solid var(--border);
    border-radius: 8px;
    box-shadow: 0 4px 14px rgb(0 0 0 / 0.1);
    font-size: 13px;
    z-index: 4;
  }
  button {
    background: none;
    border: none;
    padding: 2px 4px;
    border-radius: 4px;
    color: var(--accent);
    font: inherit;
    cursor: pointer;
  }
  button:hover { background: var(--hover); }
  .sep { color: var(--muted); }
  .current { font-weight: 600; white-space: nowrap; overflow: hidden; text-overflow: ellipsis; }
  .scale {
    margin-left: 6px;
    padding: 1px 6px;
    border-radius: 8px;
    background: var(--hover);
    color: var(--muted);
    font-weight: 400;
    font-size: 11px;
  }
</style>
