<script lang="ts">
  import { ago, dateTime } from "../format";
  import { laneColor } from "../metro/geometry";
  import { repo } from "../repo.svelte";

  const branch = $derived(
    repo.selection?.kind === "branch" ? repo.stats.find((s) => s.name === (repo.selection as { name: string }).name) : undefined,
  );
  const details = $derived(repo.selection?.kind === "commit" ? repo.details : null);
  // Everything after the summary line.
  const body = $derived(details ? details.message.split("\n").slice(1).join("\n").trim() : "");

  let copied = $state(false);
  async function copy(text: string) {
    try {
      await navigator.clipboard.writeText(text);
      copied = true;
      setTimeout(() => (copied = false), 1200);
    } catch {
      /* clipboard blocked; nothing to do */
    }
  }
</script>

{#if details || branch}
  <section class="panel" aria-label="Ayrıntılar">
    <button class="close" onclick={() => repo.clearSelection()} aria-label="Kapat">×</button>

    {#if details}
      <div class="head">
        <button class="hash" onclick={() => copy(details.id)} title="Tam hash'i kopyala">
          {copied ? "kopyalandı" : details.shortId}
        </button>
        <h3>{details.summary}</h3>
      </div>
      <dl>
        <dt>Yazar</dt>
        <dd>{details.author.name} <span class="muted">&lt;{details.author.email}&gt;</span> · {dateTime(details.author.time)} <span class="muted">({ago(details.author.time)})</span></dd>
        {#if details.committer.name !== details.author.name || details.committer.time !== details.author.time}
          <dt>Committer</dt>
          <dd>{details.committer.name} · {dateTime(details.committer.time)}</dd>
        {/if}
        <dt>Hat</dt>
        <dd>{details.lane}</dd>
        <dt>Parent</dt>
        <dd>
          {#each details.parents as p (p.id)}
            {#if p.column !== null}
              <button class="link mono" onclick={() => repo.selectCommit(p.column!, true)}>{p.shortId}</button>
            {:else}
              <span class="mono muted" title="Yüklenen geçmişin dışında">{p.shortId}</span>
            {/if}
          {:else}
            <span class="muted">yok — ilk commit</span>
          {/each}
        </dd>
        <dt>Dallar</dt>
        <dd>{details.branches.join(", ") || "—"}</dd>
        {#if details.tags.length}
          <dt>Tag</dt>
          <dd>{details.tags.join(", ")}</dd>
        {/if}
      </dl>
      {#if body}<pre class="body">{body}</pre>{/if}
    {:else if branch}
      {@const color = branch.lane !== null && repo.map ? laneColor(repo.map.lanes[branch.lane].color) : null}
      <div class="head">
        {#if color}<span class="swatch" style:background={color}></span>{/if}
        <h3>{branch.name}</h3>
        <button class="link" onclick={() => (repo.scrollTarget = branch.tipColumn)}>ucuna git →</button>
      </div>
      <dl>
        <dt>Son aktivite</dt>
        <dd>{dateTime(branch.lastActivity)} <span class="muted">({ago(branch.lastActivity)})</span></dd>
        <dt>Kendi commit'i</dt>
        <dd>{branch.commitCount}{#if branch.commitCount === 0} <span class="muted">— başka bir hattın istasyonunda duruyor</span>{/if}</dd>
        <dt>Ana dala göre</dt>
        <dd>
          {branch.ahead} önde, {branch.behind} geride
          {#if branch.merged} · <strong>merge edilmiş</strong>{/if}
          {#if branch.stale} · <strong class="stale">bayat</strong>{/if}
        </dd>
        {#if branch.upstream}
          <dt>Remote</dt>
          <dd>{branch.upstream.name}: {branch.upstream.ahead} önde, {branch.upstream.behind} geride</dd>
        {/if}
        {#if branch.authors.length}
          <dt>Yazarlar</dt>
          <dd>{branch.authors.join(", ")}</dd>
        {/if}
      </dl>
      {#if repo.map?.truncated}<p class="muted note">Geçmiş kesildiği için sayılar alt sınırdır.</p>{/if}
    {/if}
  </section>
{/if}

<style>
  .panel {
    position: relative;
    flex: none;
    max-height: 40%;
    overflow-y: auto;
    padding: 12px 16px 14px;
    background: var(--surface);
    border-top: 1px solid var(--border);
    font-size: 13px;
  }
  .close {
    position: absolute;
    top: 8px;
    right: 10px;
    font-size: 18px;
    line-height: 1;
    color: var(--muted);
    background: none;
    border: none;
    cursor: pointer;
  }
  .head { display: flex; align-items: center; gap: 10px; margin-bottom: 8px; padding-right: 24px; }
  h3 { margin: 0; font-size: 14px; font-weight: 600; }
  .hash {
    font: 12px ui-monospace, "Cascadia Code", Consolas, monospace;
    padding: 2px 8px;
    border: 1px solid var(--border);
    border-radius: 6px;
    background: var(--bg);
    color: var(--text);
    cursor: pointer;
    min-width: 74px;
  }
  .swatch { width: 18px; height: 5px; border-radius: 3px; }
  dl { display: grid; grid-template-columns: max-content 1fr; gap: 3px 16px; margin: 0; }
  dt { color: var(--muted); }
  dd { margin: 0; display: flex; flex-wrap: wrap; gap: 0 8px; align-items: baseline; }
  .muted { color: var(--muted); }
  .mono { font-family: ui-monospace, "Cascadia Code", Consolas, monospace; font-size: 12px; }
  .link { background: none; border: none; padding: 0; color: var(--accent); cursor: pointer; font: inherit; }
  .link.mono { font-family: ui-monospace, "Cascadia Code", Consolas, monospace; font-size: 12px; }
  .body { margin: 10px 0 0; white-space: pre-wrap; font: 12px/1.5 ui-monospace, "Cascadia Code", Consolas, monospace; color: var(--text-secondary); }
  .note { margin: 8px 0 0; font-size: 12px; }
  .stale { color: #a06a00; }
</style>
