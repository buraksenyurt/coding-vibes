<script lang="ts">
  import { repo } from "./repo.svelte";

  // Informational notices fade out on their own; errors stay until closed.
  $effect(() => {
    const notice = repo.notice;
    if (!notice || notice.tone !== "info") return;
    const timer = setTimeout(() => {
      if (repo.notice === notice) repo.dismissNotice();
    }, 8000);
    return () => clearTimeout(timer);
  });
</script>

{#if repo.notice}
  <div class="notice" class:error={repo.notice.tone === "error"} role={repo.notice.tone === "error" ? "alert" : "status"}>
    <span class="icon" aria-hidden="true">{repo.notice.tone === "error" ? "!" : "i"}</span>
    <span class="text">{repo.notice.text}</span>
    <button onclick={() => repo.dismissNotice()} aria-label="Kapat">×</button>
  </div>
{/if}

<style>
  .notice {
    display: flex;
    align-items: center;
    gap: 10px;
    padding: 8px 12px;
    background: var(--surface);
    border-bottom: 1px solid var(--border);
    box-shadow: inset 3px 0 0 var(--accent);
    font-size: 13px;
    color: var(--text);
  }
  .notice.error { box-shadow: inset 3px 0 0 var(--danger); }
  .icon {
    flex: none;
    display: grid;
    place-items: center;
    width: 18px;
    height: 18px;
    border-radius: 50%;
    font-size: 11px;
    font-weight: 700;
    color: var(--surface);
    background: var(--accent);
  }
  .error .icon { background: var(--danger); }
  .text { flex: 1; }
  button {
    font-size: 18px;
    line-height: 1;
    color: var(--muted);
    background: none;
    border: none;
    cursor: pointer;
  }
</style>
