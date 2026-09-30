# Faz 6 — Etkileşim ve Bilgi Panelleri *(MVP)*

> **Önceki:** [05 — Geçişler ve renkler](05-gecisler-ve-renkler.md) · **Sonraki:** 07 — Canlı izleme *(planlı)*

## 1. Bu bölümde ne yapacağız

Planın 7. bölümündeki "üç katmanlı bilgi" fikrini hayata geçirip MVP'yi tamamlayacağız:

| Katman | Nerede | Ne |
|---|---|---|
| **1 — Hep görünür** | Sol kenar çubuğu | Dal listesi (klasörlere gruplanmış), ana dala göre ↑önde ↓geride, merge edildi ✓, **bayat** rozeti, remote durumu ☁ |
| **2 — Hover** | Fareyi izleyen kutu | İstasyon: hash, yazar, ne zaman, özet. Geçiş: tür, kaynak → hedef |
| **3 — Tıklama** | Alt detay paneli | Commit: tam mesaj, yazar/committer, parent'lar (tıklanabilir), içeren dallar, tag'ler. Dal: son aktivite, commit sayısı, yazarlar, remote farkı |

Ayrıca: dal gizle/göster, dala tıklayınca haritada o dalı öne çıkarma ve ucuna kaydırma, `Esc` ile seçimi kapatma.

Rust tarafında: **ahead/behind**, **merged**, **bayat dal** ve **"bu commit hangi dallarda"** hesapları; iki yeni command: `get_commit_details`, `get_branch_stats`.

## 2. Neden böyle

### Ahead/behind nasıl hesaplanır?

`git rev-list --count main..feature/x` sorusu şudur: *feature/x'ten erişilebilen ama main'den erişilemeyen commit sayısı.* Bizim elimizde zaten tüm pencere bir graf olarak var; iki **erişilebilirlik kümesi** çıkarıp farkını saymak yeterli:

```
reach(feature/x) = { x'in ucundan parent'lar boyunca gidilebilen tüm commit'ler }
ahead  = | reach(feature/x) − reach(main) |
behind = | reach(main) − reach(feature/x) |
merged = feature/x'in ucu ∈ reach(main)
```

Küme olarak `HashSet` değil **`Vec<bool>`** kullanıyoruz: sütunlar 0'dan N'ye yoğun indeksler (Faz 2'nin arena kararı yine işimize yarıyor). Bit dizisi hem daha hızlı hem de "ahead" hesabı basit bir `zip` + `filter`.

Pencere dışındaki geçmiş bu hesaba girmez; geçmiş kesilmişse (`truncated`) sayılar **alt sınırdır**. Panel bunu kullanıcıya söylüyor.

### "Bayat dal" tanımı

*Merge edilmemiş* ve son commit'i **30 günden eski** dal. Merge edilmiş eski dallar bayat değil — işini bitirmiş. Karşılaştırma için "şimdi"yi fonksiyona parametre olarak veriyoruz (`branch_stats(map, now, stale_days)`); test yazarken saati dondurmanın en temiz yolu bu.

### Neden haritayı önbellekte tutuyoruz?

Detay paneli her tıklamada açılıyor. Her seferinde repo'yu baştan yürümek israf; `get_metro_map` ürettiği `MetroMap`'i `AppState` içinde bir `Arc` olarak saklıyor, `get_commit_details` ve `get_branch_stats` o haritadan cevap veriyor. Yeni repo açılınca önbellek temizleniyor.

### Tooltip ve seçim durumu nerede yaşar?

- **Hover** geçici ve sadece haritayı ilgilendiriyor → `MetroView` içinde yerel `$state`.
- **Seçim** (commit ya da dal) haritayı, kenar çubuğunu ve paneli ilgilendiriyor → paylaşılan `repo` state'inde.

Kural: state'i onu okuyan en küçük ortak ataya koy.

## 3. Adım adım uygulama

### 3.1 Core: `stats.rs`

```rust
pub fn reachable(map: &MetroMap, tip: Column) -> Vec<bool> {
    let mut seen = vec![false; map.commits.len()];
    let mut stack = vec![tip];
    while let Some(column) = stack.pop() {
        if std::mem::replace(&mut seen[column], true) {
            continue;
        }
        stack.extend(map.parents_of(column).filter(|&p| !seen[p]));
    }
    seen
}
```

`std::mem::replace` eski değeri döndürüp yenisini yazıyor: "daha önce gördüm mü?" sorusu ve "gördüm diye işaretle" işlemi tek satırda. Özyineleme yerine açık bir `stack` — binlerce commit'lik zincirde stack overflow riski yok.

```rust
pub struct BranchStats {
    pub name: String,
    pub is_remote_only: bool,
    pub lane: Option<LaneId>,
    pub tip_column: Column,
    pub ahead: usize,
    pub behind: usize,
    pub merged: bool,
    pub stale: bool,
    pub last_activity: i64,
    pub commit_count: usize,
    pub authors: Vec<String>,
    pub upstream: Option<Upstream>,   // origin/x ile karşılaştırma
}
```

Karşılaştırma tabanı `base_branch`: `main`, yoksa `master`, yoksa `HEAD`'in dalı. Yerelde ikizi olan remote dal (`origin/main`) ayrı satır olarak değil, ikizinin `upstream` alanı olarak raporlanıyor — kenar çubuğunda aynı dal iki kez görünmesin.

`branches_containing(map, column)`: her dal ucundan `reachable` hesaplayıp bu commit'i içerenleri listeliyor. Küçük bir optimizasyon: ucu commit'ten *önceki* bir sütunda olan dal onu içeremez (`*tip >= column`), çünkü sütunlar topolojik sırada.

**Fixture testi bir yanılgımızı düzeltti.** Şunu yazmıştık:

```rust
// Develop has commits main never got
assert!(get("develop").ahead > 0);
```

Test kırıldı. Sebep: `release/1.0`, `develop`'un **ucundan** açılıp `main`'e merge edildi; yani `main` artık `develop`'un her şeyini içeriyor. `develop` 0 önde ve *merged*. Algoritma doğruydu, bizim hikâyeyi okuyuşumuz yanlıştı. Test şimdi bunu belgeliyor:

```rust
let develop = get("develop");
assert_eq!(develop.ahead, 0);
assert!(develop.merged);
assert!(develop.behind > 0);
```

### 3.2 Tauri: önbellek ve yeni command'lar

`src-tauri/src/state.rs`:

```rust
pub struct AppState {
    source: Mutex<Option<Arc<GixSource>>>,
    map: Mutex<Option<Arc<MetroMap>>>,
}
```

`src-tauri/src/commands.rs`:

```rust
#[tauri::command]
pub fn get_commit_details(id: String, state: State<'_, AppState>) -> Result<CommitDetailsDto, AppError> {
    let map = cached_map(&state)?;
    let column = id.parse().ok()
        .and_then(|id| map.column_of(&id))
        .ok_or_else(|| AppError::new(ErrorKind::ReadFailed, format!("commit {id} is not on the map")))?;
    Ok(CommitDetailsDto::new(&map, column))
}

#[tauri::command]
pub fn get_branch_stats(state: State<'_, AppState>) -> Result<Vec<BranchStatsDto>, AppError> {
    let map = cached_map(&state)?;
    let now = SystemTime::now().duration_since(UNIX_EPOCH).map(|d| d.as_secs() as i64).unwrap_or(0);
    Ok(branch_stats(&map, now, DEFAULT_STALE_DAYS).iter().map(BranchStatsDto::from).collect())
}
```

Bu ikisi **senkron** command: önbellekten okuyup hesaplıyorlar, milisaniyeler sürüyor. `spawn_blocking` gerekmiyor.

`id.parse()`: Faz 1'de `CommitId` için yazdığımız `FromStr` impl'i burada karşılığını veriyor — hex string'i doğrulayıp 20 byte'a çeviriyor.

### 3.3 Demo paketi

Demo modu artık sadece haritayı değil, dal istatistiklerini ve her commit'in detayını da içeren bir `DemoBundle` dosyası kullanıyor (`src/lib/demo/demo-bundle.json`). Üretirken "şimdi"yi fixture'ın son commit'inden bir hafta sonraya sabitliyoruz; demo hiçbir zaman "bayat" görünmez:

```powershell
cargo test -p easy-git -- --ignored export_demo_map
```

### 3.4 Ön yüz state'i

`repo.svelte.ts`'e eklenenler:

```ts
export type Selection = { kind: "commit"; column: number } | { kind: "branch"; name: string } | null;

class RepoState {
  stats = $state<BranchStatsDto[]>([]);
  selection = $state<Selection>(null);
  details = $state<CommitDetailsDto | null>(null);
  hiddenLanes = $state<Set<number>>(new Set());
  scrollTarget = $state<number | null>(null);

  toggleLane(lane: number) {
    const next = new Set(this.hiddenLanes);
    if (next.has(lane)) next.delete(lane);
    else next.add(lane);
    this.hiddenLanes = next;
  }
}
```

`toggleLane`'de `Set`'i yerinde değiştirmek yerine **yeni bir `Set`** atıyoruz. Svelte 5 `$state` içindeki düz nesne ve dizileri derinlemesine izler ama `Set`/`Map` için `svelte/reactivity`'deki `SvelteSet` gerekir; yeni nesne atamak ikisine de gerek bırakmayan basit bir yol.

`scrollTarget` bir "istek kutusu": panel ya da kenar çubuğu bir sütuna gidilmesini istiyor, harita isteği yerine getirip kutuyu boşaltıyor. Bileşenler birbirini tanımadan haberleşiyor.

Yenile (⟳) sonrası seçim korunuyor: seçili commit'in id'si yeni haritada aranıp sütunu güncelleniyor (`restoreSelection`).

### 3.5 Kenar çubuğu — `panels/Sidebar.svelte`

- `feature/login` ve `feature/payments` → `feature/` klasörü altında `login`, `payments`. Klasörler açılıp kapanabiliyor.
- Rozetler: `↑2 ↓11` (ana dala göre), `✓` (merge edilmiş), `bayat`, `☁ ↑1` (remote'a göre). Her rozetin `title`'ı cümle olarak açıklıyor.
- Kendi commit'i olmayan dal (Faz 2'de şerit açmayan) içi boş bir daireyle gösteriliyor.
- Satırın sağında göz düğmesi: hattı gizle/göster.

Rozetlerdeki sayılar `font-variant-numeric: tabular-nums` ile hizalı; alt alta duran sayılarda rakam genişliklerinin sabit olması okumayı kolaylaştırıyor.

### 3.6 Haritada etkileşim — `MetroView.svelte`

**Gizli hatlar ve satırlar.** Gizlenen hattın satırı boş kalmasın diye satırlar yeniden numaralanıyor:

```ts
const rowOfLane = $derived.by(() => {
  const raw = map.lanes.map((lane) => (view.compact ? lane.compactRow : lane.row));
  const used = [...new Set(map.lanes.filter((l) => visible(l.id)).map((l) => raw[l.id]))].sort((a, b) => a - b);
  const renumber = new Map(used.map((row, i) => [row, i]));
  return raw.map((row) => renumber.get(row) ?? -1);
});
```

Aynı kod hem normal hem kompakt modda çalışıyor.

**İstasyon isabet alanı.** Faz 5'teki geçiş ikizinin aynısı: istasyonun üstünde 11px yarıçaplı şeffaf bir daire. Hover'da tooltip, tıklamada `repo.selectCommit(column)`. Seçili istasyonun etrafında vurgu halkası.

**Odak.** Faz 5'te sadece hover edilen geçiş için vardı; şimdi seçili dal da odak üretiyor:

```ts
const focus = $derived.by(() => {
  if (hovered !== null) { /* geçiş: iki hat, iki istasyon */ }
  if (repo.selection?.kind === "branch") { /* dalın hattı ve tüm istasyonları */ }
  return null;
});
```

Hover seçimden önce geliyor: dal seçiliyken bile bir geçişin üzerine gelince o geçiş öne çıkıyor.

**Kaydırma isteği:**

```ts
$effect(() => {
  const target = repo.scrollTarget;
  if (target === null || !scroller) return;
  scroller.scrollTo({ left: x(target) - (scroller.clientWidth - LABEL_WIDTH) / 2, behavior: "smooth" });
  repo.scrollTarget = null;
});
```

Hedef sütun, etiket sütunu hariç görünür alanın ortasına geliyor.

**Tooltip — `metro/Tooltip.svelte`:** `position: fixed`, fareyi 14px sağ-alttan izliyor; pencere kenarına taşacaksa öbür tarafa geçiyor. `pointer-events: none` — tooltip'in kendisi fareyi yakalayıp hover'ı bozmasın.

### 3.7 Detay paneli — `panels/DetailPanel.svelte`

Commit seçiliyken: kısa hash (tıklayınca tam hash panoya kopyalanır), özet, yazar ve — farklıysa — committer, hat, **tıklanabilir parent'lar** (pencere dışındaki parent soluk ve tıklanamaz), içeren dallar, tag'ler ve mesaj gövdesi.

Dal seçiliyken: son aktivite, kendi commit sayısı, ana dala göre durum, remote farkı, yazarlar ve "ucuna git →".

Parent'a tıklamak `repo.selectCommit(column, true)` çağırıyor; ikinci parametre kaydırma isteği. Merge commit'ten başlayıp parent'lar arasında gezinerek bir dalın hikâyesini geriye doğru okumak mümkün.

### 3.8 Yerleşim — `App.svelte`

```
┌──────────────────────── Toolbar ────────────────────────┐
├─────────┬───────────────────────────────────────────────┤
│ Sidebar │  MetroView (+ Legend, Tooltip)                │
│         ├───────────────────────────────────────────────┤
│         │  DetailPanel (seçim varsa)                    │
└─────────┴───────────────────────────────────────────────┘
```

Flexbox ile; detay paneli en fazla yüksekliğin %40'ını alıyor, harita kalan alanı dolduruyor.

### 3.9 Gerçek uygulamada doğrulama

Demo modu tarayıcıda hızlı çalışmak için harika ama IPC yolunu sınamıyor. Faz sonunda uygulamanın kendisi (WebKitGTK ile Linux'ta, sanal ekranda) fixture repo'yu gerçekten gix ile okuyup çizdi. Bu deneme Faz 5'teki ok başı sorununu da ortaya çıkardı (bkz. Faz 5, 3.4). Windows'ta aynı denemeyi yapmak için:

```powershell
cargo run -p easy-git-repo --example make-sample-repo -- C:\temp\easy-git-sample
npm run tauri dev
# Repo seç → C:\temp\easy-git-sample\sample
```

## 4. Takıldığın yerler

| Belirti | Sebep / çözüm |
|---|---|
| Hat gizleme düğmesi çalışmıyor | `Set` yerinde değiştirilmiş (`hiddenLanes.add(...)`); yeni `Set` ata ya da `SvelteSet` kullan. |
| Tooltip titriyor | Tooltip fareyi yakalıyor; `pointer-events: none` eksik. |
| `get_commit_details` → "no repository is open" | Önbellek boş; `get_metro_map` önce çağrılmalı. `repo.open` bu sırayı garanti ediyor. |
| Tüm dallar "bayat" | Demo verisi gerçek "şimdi" ile karşılaştırılıyor. Demo paketini `export_demo_map` ile yeniden üret. |
| `develop` "merge edilmiş" görünüyor | Doğru olabilir: `main` `develop`'un ucunu içeriyorsa (ör. release dalı üzerinden) öyledir. |

## 5. Ne öğrendik

- Erişilebilirlik kümeleriyle ahead/behind/merged; yoğun indeksler için `Vec<bool>`.
- Zamanı parametre yapıp testte dondurmak.
- Tauri tarafında hesaplanmış sonucu `Arc` ile önbelleğe almak; senkron ve asenkron command seçimi.
- Svelte 5'te paylaşılan ve yerel state ayrımı; `Set` reaktivitesi; "istek kutusu" ile bileşenler arası haberleşme.
- Fareyi izleyen, kenarda yön değiştiren tooltip.
- Demo modunun sınırı: IPC ve WebView farklarını ancak gerçek uygulama gösterir.

## 6. Kendin dene

1. Kenar çubuğuna bir arama kutusu ekle; yazdıkça dal listesi süzülsün.
2. Detay paneline "bu commit'te değişen dosya sayısı" ekle. İpucu: gix ile commit'in tree'sini ilk parent'ınkiyle karşılaştır (`tree.changes()`); bu yeni bir command ve `spawn_blocking` gerektirir.
3. Klavye ile gezinme: seçili commit varken `←`/`→` aynı hattaki önceki/sonraki istasyona geçsin.

---

## Ek — Taşınmış ya da silinmiş repolar

Son açılanlar listesi bir yol listesi; yollar zamanla bayatlar. Repo taşınır, klasörün adı değişir ya da silinir. Kural basit: **listeden açılmaya çalışılan bir repo artık açılamıyorsa kullanıcıya nedenini söyle ve listeden çıkar.**

**Rust tarafı.** `open_repository` iki durumu ayırt ediyor: klasör yok (`ErrorKind::NotFound`) ve klasör var ama git reposu değil (`ErrorKind::NotARepository`). İkisinde de yol listeden düşürülüyor:

```rust
let source = match opened {
    Ok(source) => source,
    Err(err) => {
        if matches!(err.kind, ErrorKind::NotFound | ErrorKind::NotARepository) {
            recent::forget(&app, &path);
        }
        return Err(err);
    }
};
```

`recent::forget`, Faz 3'teki `push_front` gibi saf bir yardımcıya (`without`) dayanıyor ve Windows yollarının büyük/küçük harf duyarsızlığını hesaba katıyor (`C:\Repo` = `c:\repo`). Kendi birim testi var.

**Ön yüz tarafı.** Hata artık sadece mesaj değil, **türünü** de taşıyor. `api.ts`'teki `ApiError`, Rust'ın gönderdiği `kind` alanını saklıyor; `ErrorKind` tipi `ts-rs` ile üretildiği için yeni tür (`"notFound"`) TypeScript'e kendiliğinden geldi.

`repo.openRecent(entry)` bir hata alınca:

- Tür `notFound` ya da `notARepository` ise bir **bilgi şeridi** gösteriyor: *"“eski-proje” açılamadı: klasör bulunamadı (taşınmış, adı değişmiş ya da silinmiş olabilir). Son kullanılanlar listesinden çıkarıldı."* Ardından listeyi Rust'tan yeniden okuyor.
- Başka bir hata ise ve ekranda zaten bir harita varsa, harita yerinde kalıyor; hata aynı şeritte kırmızı çizgiyle gösteriliyor. Eskiden her hata haritayı kaldırıp tam ekran hata sayfası açıyordu.

`NoticeBar.svelte` araç çubuğunun hemen altında duruyor. Bilgi mesajları 8 saniye sonra kendiliğinden kayboluyor; hata mesajları kapatılana kadar kalıyor. Zamanlayıcı bir `$effect` içinde kuruluyor ve efektin döndürdüğü temizleme fonksiyonu (`clearTimeout`) yeni bir mesaj geldiğinde eskisinin zamanlayıcısını iptal ediyor.

**Kendin dene:** menüdeki her satıra bir "×" düğmesi ekleyip listeden elle çıkarmayı sağla. Rust'ta `recent::forget` hazır; ona bir command yazman ve capability'lere dokunmadan (kendi command'ların zaten izinli) çağırman yeterli.
