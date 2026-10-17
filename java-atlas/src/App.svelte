<script lang="ts">
  import { appInfo, type AppInfo } from "./lib/api";
  import { project } from "./lib/project.svelte";
  import Toolbar from "./lib/Toolbar.svelte";
  import ScanOverlay from "./lib/ScanOverlay.svelte";
  import ReportPanel from "./lib/panels/ReportPanel.svelte";
  import CountryPreview from "./lib/panels/CountryPreview.svelte";

  let info = $state<AppInfo | null>(null);

  $effect(() => {
    appInfo().then((i) => (info = i)).catch(() => (info = null));
    project.init().then(() => {
      // Development convenience: `VITE_AUTO_OPEN=/path/to/project npm run tauri dev`
      // opens a project at start-up, skipping the folder picker.
      const auto = import.meta.env.VITE_AUTO_OPEN as string | undefined;
      if (auto) project.open(auto);
    });
  });
</script>

<div class="shell">
  <Toolbar />

  <main>
    {#if project.summary}
      <div class="workspace">
        <ReportPanel />
        <CountryPreview />
      </div>
    {:else}
      <div class="sea">
        <svg class="map" viewBox="0 0 800 480" role="img" aria-label="Boş atlas">
          <path class="land" d="M250 150 Q320 90 420 120 T560 170 Q600 260 520 330 T330 340 Q220 290 250 150 Z" />
          <text x="400" y="245" text-anchor="middle" class="hint">Bir Java projesi seçerek başla</text>
        </svg>
      </div>
    {/if}

    {#if project.status === "scanning"}
      <ScanOverlay />
    {/if}

    {#if project.error}
      <div class="error" role="alert">
        <span>{project.error}</span>
        <button class="ghost" onclick={() => (project.error = null)} aria-label="Kapat">✕</button>
      </div>
    {/if}
  </main>

  <footer class="status">
    {#if info}
      <span>java-atlas {info.version} · {info.platform}</span>
      <span class={info.javaGrammarOk ? "ok" : "bad"}>tree-sitter-java {info.javaGrammarOk ? "hazır" : "yüklenemedi"}</span>
    {:else}
      <span>Rust tarafı bekleniyor…</span>
    {/if}
  </footer>
</div>

<style>
  .shell { display: grid; grid-template-rows: auto 1fr auto; height: 100%; }
  main { position: relative; min-height: 0; }
  .workspace { display: grid; grid-template-columns: minmax(320px, 400px) 1fr; height: 100%; min-height: 0; }
  .sea { display: grid; place-items: center; height: 100%; }
  .map { width: min(90%, 900px); height: auto; }
  .land { fill: var(--land); stroke: var(--land-edge); stroke-width: 2; }
  .hint { fill: var(--muted); font-size: 16px; }
  .status {
    display: flex;
    justify-content: space-between;
    padding: 6px 14px;
    background: var(--panel);
    border-top: 1px solid var(--border);
    color: var(--muted);
    font-size: 12px;
  }
  .ok { color: var(--ok); }
  .bad { color: var(--bad); }
  .error {
    position: absolute;
    left: 50%;
    bottom: 16px;
    transform: translateX(-50%);
    display: flex;
    gap: 12px;
    align-items: center;
    max-width: 80%;
    padding: 10px 14px;
    background: var(--panel);
    border: 1px solid var(--bad);
    border-left: 4px solid var(--bad);
    border-radius: 8px;
    box-shadow: 0 8px 24px rgb(0 0 0 / 0.18);
    z-index: 30;
  }
</style>
