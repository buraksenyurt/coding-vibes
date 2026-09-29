<script lang="ts">
  import { onMount } from "svelte";
  import Toolbar from "./lib/Toolbar.svelte";
  import MetroView from "./lib/metro/MetroView.svelte";
  import DetailPanel from "./lib/panels/DetailPanel.svelte";
  import Sidebar from "./lib/panels/Sidebar.svelte";
  import { repo } from "./lib/repo.svelte";

  onMount(() => {
    repo.loadRecent();
  });
</script>

<div class="shell">
  <Toolbar />

  {#if repo.map && !repo.error}
    <div class="body">
      <Sidebar />
      <div class="stage">
        <main>
          <MetroView map={repo.map} />
          {#if repo.loading}<div class="busy">Yenileniyor…</div>{/if}
        </main>
        <DetailPanel />
      </div>
    </div>
  {:else}
    <main>
      {#if repo.error}
        <div class="message error">
          <p>{repo.error}</p>
          <button onclick={() => (repo.error = null)}>Tamam</button>
        </div>
      {:else if repo.loading}
        <div class="message">Repository okunuyor…</div>
      {:else}
        <div class="message empty">
          <h1>easy-git</h1>
          <p>Başlamak için bir repository seç.</p>
        </div>
      {/if}
    </main>
  {/if}
</div>

<style>
  .shell { height: 100%; display: flex; flex-direction: column; }
  .body { flex: 1; min-height: 0; display: flex; }
  .stage { flex: 1; min-width: 0; display: flex; flex-direction: column; }
  main { flex: 1; min-height: 0; position: relative; }
  .message { height: 100%; display: grid; place-content: center; justify-items: center; text-align: center; color: var(--muted); }
  .error { color: var(--danger); }
  .error button { font: inherit; padding: 4px 14px; border-radius: 6px; border: 1px solid var(--border); background: var(--surface); color: var(--text); cursor: pointer; }
  h1 { color: var(--text); margin: 0 0 8px; font-weight: 600; }
  .busy {
    position: absolute;
    top: 10px;
    left: 50%;
    transform: translateX(-50%);
    z-index: 5;
    padding: 4px 12px;
    border-radius: 12px;
    background: var(--surface);
    border: 1px solid var(--border);
    font-size: 12px;
    color: var(--text-secondary);
  }
</style>
