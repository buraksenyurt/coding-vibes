<script lang="ts">
  import { project, stageFraction, stageLabel } from "./project.svelte";
  import { baseName } from "./format";

  const fraction = $derived(stageFraction(project.progress));
</script>

<div class="veil" role="status" aria-live="polite">
  <div class="card">
    <div class="name">{project.path ? baseName(project.path) : ""}</div>
    <div class="bar" aria-hidden="true"><div class="fill" style:width="{Math.round(fraction * 100)}%"></div></div>
    <div class="label">{stageLabel(project.progress)}</div>
  </div>
</div>

<style>
  .veil {
    position: absolute;
    inset: 0;
    display: grid;
    place-items: center;
    background: color-mix(in srgb, var(--sea) 70%, transparent);
    backdrop-filter: blur(2px);
    z-index: 10;
  }
  .card {
    width: min(420px, 90%);
    padding: 20px 22px;
    background: var(--panel);
    border: 1px solid var(--border);
    border-radius: 12px;
    box-shadow: 0 12px 32px rgb(0 0 0 / 0.18);
  }
  .name { font-weight: 600; margin-bottom: 12px; }
  .bar { height: 8px; background: var(--hover); border-radius: 4px; overflow: hidden; }
  .fill { height: 100%; background: var(--accent); border-radius: 4px; transition: width 120ms linear; }
  .label { margin-top: 10px; color: var(--muted); font-size: 13px; font-variant-numeric: tabular-nums; }
</style>
