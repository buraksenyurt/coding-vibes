<script lang="ts">
  import { project } from "../project.svelte";
  import AtlasTree from "./AtlasTree.svelte";
  import ReportPanel from "./ReportPanel.svelte";

  const problems = $derived(
    project.summary ? project.summary.cycles.length + project.summary.violations.length + project.summary.findings.length : 0,
  );
</script>

<aside class="side">
  <div class="tabs" role="tablist" aria-label="Yan panel">
    <button role="tab" aria-selected={project.sideTab === "atlas"} class:active={project.sideTab === "atlas"} onclick={() => (project.sideTab = "atlas")}>Atlas</button>
    <button role="tab" aria-selected={project.sideTab === "report"} class:active={project.sideTab === "report"} onclick={() => (project.sideTab = "report")}>
      Rapor {#if problems}<span class="count">{problems}</span>{/if}
    </button>
  </div>
  <div class="body" role="tabpanel">
    {#if project.sideTab === "atlas"}
      <AtlasTree />
    {:else}
      <ReportPanel />
    {/if}
  </div>
</aside>

<style>
  .side { display: grid; grid-template-rows: auto 1fr; min-height: 0; background: var(--panel); border-right: 1px solid var(--border); }
  .tabs { display: flex; gap: 2px; padding: 6px 8px 0; border-bottom: 1px solid var(--border); }
  .tabs button {
    padding: 7px 12px;
    background: none;
    border: none;
    border-bottom: 2px solid transparent;
    color: var(--muted);
    font: inherit;
    font-weight: 600;
    cursor: pointer;
  }
  .tabs button.active { color: var(--ink); border-bottom-color: var(--accent); }
  .count { margin-left: 4px; padding: 0 6px; border-radius: 8px; background: var(--hover); font-size: 11px; }
  .body { min-height: 0; overflow: hidden; display: grid; }
</style>
