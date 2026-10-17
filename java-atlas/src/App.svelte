<script lang="ts">
  import { appInfo, pickProjectFolder, type AppInfo } from "./lib/api";

  let info = $state<AppInfo | null>(null);
  let error = $state<string | null>(null);
  let projectPath = $state<string | null>(null);

  $effect(() => {
    appInfo()
      .then((i) => (info = i))
      .catch((e) => (error = String(e)));
  });

  async function chooseProject() {
    try {
      const path = await pickProjectFolder();
      if (path) projectPath = path;
    } catch (e) {
      error = String(e);
    }
  }
</script>

<div class="shell">
  <header class="toolbar">
    <button class="primary" onclick={chooseProject}>📂 Proje seç</button>
    <span class="project">{projectPath ?? "Henüz proje seçilmedi"}</span>
  </header>

  <main class="sea">
    <svg class="map" viewBox="0 0 800 480" role="img" aria-label="Boş atlas">
      <!-- A placeholder island: the real country arrives in phase 5. -->
      <path class="land" d="M250 150 Q320 90 420 120 T560 170 Q600 260 520 330 T330 340 Q220 290 250 150 Z" />
      <text x="400" y="245" text-anchor="middle" class="hint">
        {projectPath ? "Harita Faz 5'te burada çizilecek" : "Bir Java projesi seçerek başla"}
      </text>
    </svg>
  </main>

  <footer class="status">
    {#if error}
      <span class="bad">Hata: {error}</span>
    {:else if info}
      <span>java-atlas {info.version} · {info.platform}</span>
      <span class={info.javaGrammarOk ? "ok" : "bad"}>
        tree-sitter-java {info.javaGrammarOk ? "hazır" : "yüklenemedi"}
      </span>
    {:else}
      <span>Rust tarafı bekleniyor…</span>
    {/if}
  </footer>
</div>
