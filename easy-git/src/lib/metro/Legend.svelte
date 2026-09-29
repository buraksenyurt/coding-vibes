<script lang="ts">
  import { view } from "../view.svelte";
  import { KIND_LABELS } from "./geometry";

  const kinds = ["fork", "merge", "cherryPick"] as const;
</script>

<aside class="legend" aria-label="Lejant">
  {#if view.colorMode === "kind"}
    {#each kinds as kind (kind)}
      <div class="row">
        <svg width="34" height="10" aria-hidden="true">
          <line x1="2" y1="5" x2={kind === "merge" ? 26 : 32} y2="5"
                stroke="var(--kind-{kind})" stroke-width="2"
                stroke-dasharray={kind === "cherryPick" ? "5 4" : undefined} />
          {#if kind === "merge"}<path d="M 26 1 L 32 5 L 26 9 z" fill="var(--kind-merge)" />{/if}
        </svg>
        <span>{KIND_LABELS[kind]}</span>
      </div>
    {/each}
  {:else}
    <div class="row"><svg width="34" height="10" aria-hidden="true"><line x1="2" y1="5" x2="32" y2="5" class="neutral" /></svg><span>Dal ayrılması — yeni dalın rengi</span></div>
    <div class="row"><svg width="34" height="10" aria-hidden="true"><line x1="2" y1="5" x2="26" y2="5" class="neutral" /><path d="M 26 1 L 32 5 L 26 9 z" class="neutral-fill" /></svg><span>Birleşme — kaynak dalın rengi</span></div>
    <div class="row"><svg width="34" height="10" aria-hidden="true"><line x1="2" y1="5" x2="32" y2="5" class="neutral" stroke-dasharray="5 4" /></svg><span>Cherry-pick — kaynak dalın rengi</span></div>
  {/if}
  <div class="row"><svg width="34" height="14" aria-hidden="true"><circle cx="17" cy="7" r="5.5" class="station-merge" /><circle cx="17" cy="7" r="2" class="station-dot" /></svg><span>Merge commit</span></div>
</aside>

<style>
  .legend {
    position: absolute;
    right: 16px;
    bottom: 16px;
    z-index: 3;
    padding: 8px 12px;
    background: var(--surface);
    border: 1px solid var(--border);
    border-radius: 8px;
    box-shadow: 0 4px 16px rgb(0 0 0 / 0.08);
    font-size: 12px;
    color: var(--text-secondary);
    display: grid;
    gap: 4px;
  }
  .row { display: flex; align-items: center; gap: 8px; }
  .neutral { stroke: var(--text-secondary); stroke-width: 2; }
  .neutral-fill { fill: var(--text-secondary); }
  .station-merge { fill: var(--surface); stroke: var(--text-secondary); stroke-width: 2; }
  .station-dot { fill: var(--text-secondary); }
</style>
