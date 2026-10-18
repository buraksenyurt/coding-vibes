<script lang="ts">
  // The right-hand drawer: everything about the selected node or road.
  // Every name in it is a link that flies the map there.
  import type { LinkDto } from "../bindings/LinkDto";
  import type { NodeKindDto } from "../bindings/NodeKindDto";
  import type { TypeKindDto } from "../bindings/TypeKindDto";
  import { copyText } from "../clipboard";
  import { count, instability } from "../format";
  import { project } from "../project.svelte";

  const sel = $derived(project.selection!);

  const kindText: Record<NodeKindDto, string> = {
    module: "Modül · bölge",
    package: "Paket · şehir",
    type: "Tip · bina",
    library: "Kütüphane · ada",
    jdk: "JDK paketi · liman",
    unknown: "Bilinmeyen paket · ada",
  };
  const kindIcon: Record<NodeKindDto, string> = { module: "▦", package: "▤", type: "▪", library: "⬭", jdk: "⚓", unknown: "?" };
  const typeText: Record<TypeKindDto, string> = {
    class: "sınıf",
    interface: "arayüz",
    enum: "enum",
    record: "record",
    annotation: "annotation",
  };
  const matchText: Record<string, string> = { jar: "jar ile doğrulandı", name: "ad benzerliğinden tahmin", mixed: "jar + tahmin" };
  const findingText = {
    unusedModule: "beyan edilmiş, kullanılmıyor",
    unusedLibrary: "beyan edilmiş, kullanılmıyor",
    undeclaredModule: "kullanılıyor, beyan edilmemiş",
    undeclaredLibrary: "kullanılıyor, beyan edilmemiş",
  } as const;

  /** Long lists are cut; the rest is one click away on the map anyway. */
  const LIMIT = 40;
  let expanded = $state<Record<string, boolean>>({});
  $effect(() => {
    void project.selection;
    expanded = {};
  });
  function limited<T>(list: T[], id: string): T[] {
    return expanded[id] ? list : list.slice(0, LIMIT);
  }

  function maxWeight(list: LinkDto[]): number {
    return Math.max(1, ...list.map((l) => l.main + l.test));
  }

  let copied = $state<string | null>(null);
  async function copy(text: string, id: string) {
    if (await copyText(text)) {
      copied = id;
      setTimeout(() => copied === id && (copied = null), 1200);
    }
  }

  function close() {
    project.clearSelection();
  }
</script>

{#snippet links(title: string, list: LinkDto[], id: string)}
  {#if list.length > 0}
    {@const max = maxWeight(list)}
    <section>
      <h3>{title} <span class="badge">{list.length}</span></h3>
      <ul class="links">
        {#each limited(list, id) as l (l.node.key)}
          <li>
            <button class="link" onclick={() => project.goToNode(l.node.key)} title={l.node.name}>
              <span class="icon">{kindIcon[l.kind]}</span>
              <span class="name">{l.node.name}</span>
              <span class="weight">{l.main}{#if l.test}<span class="muted"> +{l.test}</span>{/if}</span>
            </button>
            <span class="bar" style:width="{((l.main + l.test) / max) * 100}%" class:test-only={l.main === 0}></span>
          </li>
        {/each}
      </ul>
      {#if list.length > LIMIT && !expanded[id]}
        <button class="more" onclick={() => (expanded = { ...expanded, [id]: true })}>+{list.length - LIMIT} daha</button>
      {/if}
    </section>
  {/if}
{/snippet}

<aside class="details" aria-label="Ayrıntılar">
  <button class="close ghost" onclick={close} aria-label="Kapat" title="Kapat (Esc)">✕</button>

  {#if sel.kind === "node"}
    {@const d = sel.details}
    {@const n = d.node}
    <header>
      <div class="kind">{kindIcon[n.kind]} {kindText[n.kind]}</div>
      <h2>{n.name}</h2>
      {#if n.fqn && n.fqn !== n.name}<div class="mono muted">{n.fqn}</div>{/if}
      <div class="chips">
        {#if n.typeKind}<span class="chip">{typeText[n.typeKind]}</span>{/if}
        {#if n.isTest}<span class="chip">test</span>{/if}
        {#if n.jpms}<span class="chip">JPMS: {n.jpms}</span>{/if}
        {#if n.exported === false}<span class="chip">🔒 export edilmemiş</span>{/if}
        {#if n.exported === true}<span class="chip">export ediliyor</span>{/if}
        {#if n.inCycle}<span class="chip bad">⟲ döngüde</span>{/if}
        {#if n.kind === "library"}
          <span class="chip">{n.version ?? "sürüm yok"}</span>
          <span class="chip" class:warn={!n.matchedBy}>{n.matchedBy ? matchText[n.matchedBy] : "kodda kullanılmıyor"}</span>
        {/if}
      </div>
    </header>

    {#if d.path.length > 1}
      <nav class="path" aria-label="Konum">
        {#each d.path as p, i (p.key)}
          {#if i > 0}<span class="sep">▸</span>{/if}
          {#if i < d.path.length - 1}
            <button class="crumb" onclick={() => project.goToNode(p.key)}>{p.name}</button>
          {:else}
            <span class="crumb current">{p.name}</span>
          {/if}
        {/each}
      </nav>
    {/if}

    {#if n.kind === "module" || n.kind === "package" || n.kind === "type"}
      <div class="metrics">
        <div><strong>{count(n.size)}</strong><span>satır</span></div>
        {#if n.metrics}
          <div title="Afferent: buna bağımlı olanlar"><strong>{n.metrics.ca}</strong><span>Ca</span></div>
          <div title="Efferent: bunun bağımlı olduğu"><strong>{n.metrics.ce}</strong><span>Ce</span></div>
          <div title="Kararsızlık: Ce / (Ca + Ce)"><strong>{instability(n.metrics.instability)}</strong><span>I</span></div>
        {/if}
      </div>
    {/if}

    {#if d.file}
      <section>
        <h3>Dosya</h3>
        <div class="file">
          <span class="mono">{d.file}</span>
          <button class="ghost small" onclick={() => copy(d.file!, "file")}>{copied === "file" ? "✓" : "Kopyala"}</button>
        </div>
      </section>
    {/if}

    {#if d.jpms}
      {@const j = d.jpms}
      <section>
        <h3><span lang="en">module-info</span></h3>
        <div class="mono">{j.open ? "open " : ""}module {j.name}</div>
        {#each [["requires", j.requires], ["exports", j.exports], ["opens", j.opens], ["uses", j.uses], ["provides", j.provides]] as const as [word, lines] (word)}
          {#if lines.length > 0}
            <div class="jpms-group">
              <span class="muted" lang="en">{word}</span>
              <ul>
                {#each lines as line (line)}<li class="mono">{line}</li>{/each}
              </ul>
            </div>
          {/if}
        {/each}
      </section>
    {/if}

    {#if d.cycles.length > 0}
      <section>
        <h3>Döngüler <span class="badge bad">{d.cycles.length}</span></h3>
        {#each d.cycles as c, i (i)}
          <div class="item">
            <div class="cycle">
              ⟲
              {#each c.packages as p, k (p.key)}
                {#if k > 0}<span class="muted">⇄</span>{/if}
                <button class="inline" onclick={() => project.goToNode(p.key)}>{p.name}</button>
              {/each}
            </div>
            <div class="hint">En zayıf halka: <span class="mono">{c.weakestFrom.name} → {c.weakestTo.name}</span> ({c.weakestWeight} kullanım)</div>
          </div>
        {/each}
      </section>
    {/if}

    {#if d.violations.length > 0}
      <section>
        <h3>JPMS ihlalleri <span class="badge bad">{d.violations.length}</span></h3>
        {#each d.violations as v, i (i)}
          <div class="item">
            <div>
              ✗ <button class="inline" onclick={() => project.goToNode(v.fromModule.key)}>{v.fromModule.name}</button>
              → <button class="inline" onclick={() => project.goToNode(v.toPackage.key)}>{v.toPackage.name}</button>
            </div>
            <div class="hint">{v.kind === "notExported" ? "Paket export edilmiyor" : "Modül requires edilmemiş"}</div>
            <ul class="evidence">
              {#each v.evidence as e (e.file + e.line)}<li class="mono">{e.file}:{e.line}</li>{/each}
            </ul>
          </div>
        {/each}
      </section>
    {/if}

    {#if d.findings.length > 0}
      <section>
        <h3><span><span lang="en">Build</span> dosyası</span> <span class="badge warn">{d.findings.length}</span></h3>
        {#each d.findings as f, i (i)}
          <div class="item">
            <button class="inline" onclick={() => project.goToNode(f.module.key)}>{f.module.name}</button>
            → <button class="inline" onclick={() => project.goToNode(f.target.key)}>{f.target.name}</button>
            <div class="hint">{findingText[f.kind]}{f.kind === "undeclaredModule" ? (f.reachable ? " (dolaylı yoldan geliyor)" : " (erişilemiyor)") : ""}</div>
          </div>
        {/each}
      </section>
    {/if}

    {@render links("Bağımlı olduğu", d.outgoing, "out")}
    {@render links("Ona bağımlı olanlar", d.incoming, "in")}

    {#if d.children.length > 0}
      <section>
        <h3>{n.kind === "module" ? "Paketler" : "Tipler"} <span class="badge">{d.children.length}</span></h3>
        <ul class="children">
          {#each limited(d.children, "children") as c (c.key)}
            <li><button class="inline" onclick={() => project.goToNode(c.key)}>{c.name}</button></li>
          {/each}
        </ul>
        {#if d.children.length > LIMIT && !expanded.children}
          <button class="more" onclick={() => (expanded = { ...expanded, children: true })}>+{d.children.length - LIMIT} daha</button>
        {/if}
      </section>
    {/if}

    {#if d.packages.length > 0}
      <section>
        <h3>Sağladığı paketler <span class="badge">{d.packages.length}</span></h3>
        <ul class="children">
          {#each limited(d.packages, "packages") as p (p.package)}
            <li class="lib-pkg">
              <span class="mono">{p.package}</span>
              <span class="chip" class:warn={p.matchedBy !== "jar"}>{p.matchedBy === "jar" ? "jar" : "ad"}</span>
            </li>
          {/each}
        </ul>
        {#if d.packages.length > LIMIT && !expanded.packages}
          <button class="more" onclick={() => (expanded = { ...expanded, packages: true })}>+{d.packages.length - LIMIT} daha</button>
        {/if}
      </section>
    {/if}
  {:else}
    {@const e = sel.edge}
    <header>
      <div class="kind">{sel.to.kind === "library" || sel.to.kind === "unknown" ? "Feribot hattı" : sel.to.kind === "jdk" ? "Kıyı yolu" : "Yol"}</div>
      <h2 class="road-title">
        <button class="inline" onclick={() => project.goToNode(sel.from.key)}>{sel.from.name}</button>
        <span class="muted">→</span>
        <button class="inline" onclick={() => project.goToNode(sel.to.key)}>{sel.to.name}</button>
      </h2>
      <div class="chips">
        <span class="chip">{e.main} kullanım</span>
        {#if e.test}<span class="chip">+{e.test} testte</span>{/if}
        {#if e.inCycle}<span class="chip bad">⟲ döngünün parçası</span>{/if}
        {#if e.violation}<span class="chip bad">✗ kaçak geçiş</span>{/if}
      </div>
    </header>
    <section>
      <h3>
        Kanıtlar <span class="badge">{sel.evidence.length}</span>
        {#if sel.evidence.length > 0}
          <button class="ghost small push" onclick={() => copy(sel.evidence.map((x) => `${x.file}:${x.line}`).join("\n"), "all")}>
            {copied === "all" ? "✓ kopyalandı" : "Tümünü kopyala"}
          </button>
        {/if}
      </h3>
      {#if sel.evidence.length > 0 && sel.evidence.length < e.main + e.test}
        <p class="hint">Bir satırdaki birden çok kullanım tek kanıt olarak listelenir.</p>
      {/if}
      <ul class="evidence-list">
        {#each limited(sel.evidence, "evidence") as x, i (x.file + ":" + x.line + ":" + i)}
          <li>
            <span class="mono">{x.file}<span class="line">:{x.line}</span></span>
            <button class="ghost small" onclick={() => copy(`${x.file}:${x.line}`, `ev${i}`)} aria-label="Kopyala">{copied === `ev${i}` ? "✓" : "⧉"}</button>
          </li>
        {:else}
          <li class="muted">Kanıt bulunamadı.</li>
        {/each}
      </ul>
      {#if sel.evidence.length > LIMIT && !expanded.evidence}
        <button class="more" onclick={() => (expanded = { ...expanded, evidence: true })}>+{sel.evidence.length - LIMIT} daha</button>
      {/if}
    </section>
  {/if}
</aside>

<style>
  .details {
    position: relative;
    overflow: auto;
    padding: 14px 16px 24px;
    background: var(--panel);
    border-left: 1px solid var(--border);
    font-size: 13px;
  }
  .close { position: absolute; top: 8px; right: 8px; padding: 2px 8px; }
  header { padding-right: 28px; }
  .kind { color: var(--muted); font-size: 12px; font-weight: 600; }
  h2 { margin: 4px 0 2px; font-size: 17px; overflow-wrap: anywhere; }
  .road-title { display: flex; flex-wrap: wrap; gap: 6px; align-items: baseline; }
  .chips { display: flex; flex-wrap: wrap; gap: 4px; margin-top: 8px; }
  .chip { font-size: 11px; padding: 1px 7px; border-radius: 10px; background: var(--hover); white-space: nowrap; }
  .chip.bad, .badge.bad { background: var(--bad); color: white; }
  .chip.warn, .badge.warn { background: var(--warn); color: #222; }

  .path { display: flex; flex-wrap: wrap; gap: 4px; align-items: center; margin-top: 12px; font-size: 12px; }
  .sep { color: var(--muted); }
  .crumb { background: none; border: none; padding: 0; color: var(--accent); font: inherit; cursor: pointer; }
  .crumb.current { color: var(--ink); cursor: default; }

  .metrics { display: grid; grid-template-columns: repeat(4, 1fr); gap: 6px; margin-top: 12px; }
  .metrics div { display: flex; flex-direction: column; padding: 6px 8px; border: 1px solid var(--border); border-radius: 8px; }
  .metrics strong { font-size: 15px; font-variant-numeric: tabular-nums; }
  .metrics span { color: var(--muted); font-size: 11px; }

  section { margin-top: 16px; }
  h3 {
    display: flex;
    align-items: center;
    gap: 8px;
    margin: 0 0 6px;
    color: var(--muted);
    font-size: 12px;
    text-transform: uppercase;
    letter-spacing: 0.04em;
  }
  .badge { font-size: 11px; padding: 0 6px; border-radius: 8px; background: var(--hover); color: var(--ink); }
  .push { margin-left: auto; text-transform: none; letter-spacing: 0; }
  .mono { font-family: ui-monospace, "Cascadia Code", "JetBrains Mono", Menlo, monospace; font-size: 12px; overflow-wrap: anywhere; }
  .muted { color: var(--muted); }
  .hint { color: var(--muted); font-size: 12px; margin-top: 3px; }
  .item { padding: 7px 9px; border: 1px solid var(--border); border-radius: 8px; margin-bottom: 6px; }
  .small { padding: 2px 8px; font-size: 12px; }

  .file { display: flex; gap: 8px; align-items: flex-start; justify-content: space-between; }
  .jpms-group { margin-top: 6px; }
  .jpms-group ul { margin: 2px 0 0; padding-left: 16px; }

  .inline { background: none; border: none; padding: 0; color: var(--accent); font: inherit; cursor: pointer; text-align: left; overflow-wrap: anywhere; }
  .inline:hover, .crumb:hover:not(.current) { text-decoration: underline; }
  .cycle { display: flex; flex-wrap: wrap; gap: 4px; align-items: baseline; }

  ul { list-style: none; margin: 0; padding: 0; }
  .links li { position: relative; margin-bottom: 2px; }
  .link {
    position: relative;
    z-index: 1;
    display: flex;
    gap: 6px;
    align-items: center;
    width: 100%;
    padding: 4px 6px;
    background: none;
    border: none;
    border-radius: 5px;
    color: var(--ink);
    font: inherit;
    text-align: left;
    cursor: pointer;
  }
  .link:hover { background: var(--hover); }
  .link .icon { color: var(--muted); width: 14px; text-align: center; }
  .link .name { flex: 1; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; }
  .link .weight { font-variant-numeric: tabular-nums; }
  .bar { position: absolute; left: 0; bottom: 0; height: 2px; background: var(--accent); opacity: 0.5; border-radius: 1px; }
  .bar.test-only { background: var(--muted); }
  .children li { padding: 2px 0; }
  .lib-pkg { display: flex; justify-content: space-between; gap: 8px; }
  .evidence { margin-top: 4px; padding-left: 14px; color: var(--muted); }
  .evidence-list li { display: flex; justify-content: space-between; gap: 8px; align-items: center; padding: 3px 0; border-bottom: 1px solid var(--border); }
  .line { color: var(--accent); }
  .more { margin-top: 4px; background: none; border: none; padding: 2px 0; color: var(--accent); font: inherit; cursor: pointer; }
</style>
