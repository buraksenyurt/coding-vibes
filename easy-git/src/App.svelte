<script lang="ts">
  import { onMount } from "svelte";
  import Toolbar from "./lib/Toolbar.svelte";
  import { repo } from "./lib/repo.svelte";

  onMount(() => {
    repo.loadRecent();
  });
</script>

<div class="shell">
  <Toolbar />

  <main>
    {#if repo.error}
      <div class="message error">{repo.error}</div>
    {:else if repo.loading}
      <div class="message">Repository okunuyor…</div>
    {:else if repo.map}
      <div class="message">
        {repo.map.commits.length} commit · {repo.map.lanes.length} hat · {repo.map.transitions.length} geçiş
        {#if repo.map.truncated}<br /><small>Eski geçmiş sınır nedeniyle kesildi.</small>{/if}
      </div>
    {:else}
      <div class="message empty">
        <h1>easy-git</h1>
        <p>Başlamak için bir repository seç.</p>
      </div>
    {/if}
  </main>
</div>

<style>
  .shell { height: 100%; display: flex; flex-direction: column; }
  main { flex: 1; min-height: 0; position: relative; }
  .message { height: 100%; display: grid; place-content: center; text-align: center; color: var(--muted); }
  .error { color: #cf222e; }
  h1 { color: var(--text); margin: 0 0 8px; font-weight: 600; }
</style>
