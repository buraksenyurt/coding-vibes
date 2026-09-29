# Faz 5 — Geçişler ve Renkler

> **Önceki:** [04 — SVG ile çizim](04-svg-ile-cizim.md) · **Sonraki:** [06 — Etkileşim ve paneller](06-etkilesim-ve-paneller.md)

## 1. Bu bölümde ne yapacağız

Haritayı gerçek bir metro haritasına çevireceğiz:

- Fork, merge ve cherry-pick geçişlerini **eğrilerle** çizmek
- Geçişleri iki modda renklendirmek: **türe göre** ve **dala göre**
- Hep görünür bir **lejant**
- Bir geçişin üzerine gelince yalnızca o geçişin iki ucunu ve iki hattını öne çıkarmak
- Core'da **satır sıkıştırma** (compact rows) ve arayüzde "Kompakt" görünüm

## 2. Neden böyle

### Metro eğrisi mi, S eğrisi mi?

Git görselleştiricilerin çoğu parent'tan çocuğa tek bir çapraz çizgi ya da S eğrisi çizer. Bizim fixture'da `hotfix/crash` `main`'in 1. sütunundan ayrılıyor ama ilk commit'i 9. sütunda. 8 sütun boyunca uzanan bir S eğrisi, dalın *nerede başladığını* bulanıklaştırır.

Metro haritalarının cevabı: **dönüşü kısa tut, düz yolu uzun tut.**

```
Fork:   main  ●─╮                     Merge:  ─────────────╮
              │                                           │
hotfix        ╰────────────●          main   ─────────────●
```

- **Fork**: parent istasyondan çıkıp **bir sütun içinde** yeni satıra dön; oradan sonrası hattın kendi rayı.
- **Merge**: kaynak satırda düz ilerle, **son sütunda** dönüp merge commit'e bağlan.
- **Cherry-pick**: bir kopya; ray değil. Tek bir yumuşak S eğrisi, kesikli.

Bunun sonucu: uzun yatay parçalar artık **hattın rengini** taşıyor (Faz 4'teki ray, fork'tan sonraki sütundan merge'den önceki sütuna uzatıldı), geçiş eğrileri ise sadece kısa dönüşler. Göz önce hatları, sonra makasları okuyor.

### İki renklendirme modu

| Mod | Soru | Fork | Merge | Cherry-pick |
|---|---|---|---|---|
| **Türe göre** | "Burada ne oldu?" | yeşil | mor + ok | turuncu, kesikli |
| **Dala göre** | "Bu nereden geldi?" | yeni dalın rengi | kaynak dalın rengi + ok | kaynak dalın rengi, kesikli |

Tür renkleri (yeşil/mor/turuncu) paletteki üç rengin **tüm çiftleri** renk körlüğü simülasyonunda ayırt edilebilecek şekilde seçildi. Buna rağmen renk tek başına bilgi taşımıyor: merge'ün **ok başı**, cherry-pick'in **kesikli çizgisi** var. Renkleri ayırt edemeyen biri de türü şekilden okuyabiliyor.

### Neden hover'da soluklaştırma?

10 dallı bir repoda eğriler kesişir. "Bu merge hangi daldan geldi?" sorusunu cevaplamanın en hızlı yolu diğer her şeyi geri çekmek. İki uç istasyon ve iki hat tam opak, geri kalan %15.

### Satır sıkıştırma: aralık bölümleme

Fixture'da 7 hat 7 satır kaplıyor ama `feature/login` bittikten çok sonra `feature/payments` başlıyor. Aynı satırı paylaşabilirler. Bu klasik **aralık bölümleme (interval partitioning)** problemi:

1. Her hattın kendi satırında kapladığı aralık: fork'tan sonraki sütun → merge'den önceki sütun.
2. Hatları öncelik sırasıyla gez; her birini çakışmadığı **ilk** satıra koy.

Öncelik sırasıyla gezmek `main`'i hep 0. satırda, uzun ömürlü dalları üstte tutuyor. Fixture'da 7 satır 4'e iniyor:

```
compact row 0: main
compact row 1: develop
compact row 2: release/1.0 · hotfix/crash · feature/old-search (deleted)
compact row 3: feature/login · feature/payments
```

Sıkıştırmanın bedeli: soldaki etiket sütunu bir satırda birden fazla dal göstermek zorunda. Bu yüzden kompakt mod bir **seçenek**; varsayılan görünüm her dala kendi satırını veriyor.

## 3. Adım adım uygulama

### 3.1 Core: `compact_row`

`Lane` yapısına yeni alan:

```rust
pub struct Lane {
    pub row: usize,          // her hatta bir satır
    pub compact_row: usize,  // çakışmayan hatlar satır paylaşır
    // ...
}
```

`crates/easy-git-core/src/metro.rs`:

```rust
fn compact_rows(lanes: &mut [Lane], transitions: &[Transition]) {
    const GAP: usize = 1;

    let span = |lane: &Lane| {
        let start = lane.fork_column.map_or(lane.first_column, |fork| fork + 1).min(lane.first_column);
        let end = transitions
            .iter()
            .filter(|t| t.from_lane == lane.id && t.kind == TransitionKind::Merge)
            .map(|t| t.to_column - 1)
            .fold(lane.last_column, usize::max);
        (start, end)
    };

    let mut rows: Vec<Vec<(usize, usize)>> = Vec::new();
    for lane in lanes.iter_mut() {
        let (start, end) = span(lane);
        let free = |taken: &Vec<(usize, usize)>| {
            taken.iter().all(|&(s, e)| end + GAP < s || e + GAP < start)
        };
        let row = match rows.iter().position(free) {
            Some(row) => row,
            None => {
                rows.push(Vec::new());
                rows.len() - 1
            }
        };
        rows[row].push((start, end));
        lane.compact_row = row;
    }
}
```

Dikkat edilecek iki nokta:

- `span` closure'ı `transitions`'ı ödünç alıyor, `lanes`'i değil. `lanes.iter_mut()` ile değiştirirken aynı dilimi okumaya çalışsaydık borrow checker itiraz ederdi.
- `fold(lane.last_column, usize::max)`: "başlangıç değeri son commit, her merge için büyük olanı al". `usize::max` bir fonksiyon olarak doğrudan geçilebiliyor.

İlk denemede aralığı `[fork, merge]` olarak almıştık ve testler kırıldı: bir dalın merge ettiği sütun, sonraki dalın ayrıldığı sütunla aynı olabiliyor. Oysa her iki eğri de *ana hattın* satırında başlayıp bitiyor; bizim satırımızı işgal etmiyor. Aralığı "fork + 1 → merge − 1" olarak düzeltince beklenen paylaşım sağlandı. Test isimleri bunu belgeliyor:

```rust
#[test]
fn compact_rows_reuse_space_after_a_merge() { /* f ve g aynı compact satırda */ }

#[test]
fn overlapping_lanes_never_share_a_compact_row() { /* aynı anda açık iki dal */ }
```

`describe()` çıktısına `compact row N:` satırları eklendi; snapshot güncellendi (`cargo insta review`). DTO'ya `compactRow` alanı eklendi, `ts-rs` TypeScript tipini kendisi güncelledi.

### 3.2 Hattın rayı: `trackSpan`

`src/lib/metro/geometry.ts`:

```ts
export function trackSpan(lane: LaneDto, transitions: TransitionDto[]): { from: number; to: number } {
  const from = lane.forkColumn === null ? lane.firstColumn : Math.min(lane.firstColumn, lane.forkColumn + 1);
  const to = transitions
    .filter((t) => t.fromLane === lane.id && t.kind === "merge")
    .reduce((end, t) => Math.max(end, t.toColumn - 1), lane.lastColumn);
  return { from, to };
}
```

Core'daki `span` ile aynı mantık. İkisinin aynı kaldığından emin olmak için bir sonraki adım bu değeri DTO'ya koymak olabilir (bkz. "Kendin dene").

### 3.3 Geçiş yolu

```ts
export function transitionPath(t: TransitionDto, fromRow: number, toRow: number): string {
  const x1 = x(t.fromColumn), y1 = y(fromRow);
  const x2 = x(t.toColumn),   y2 = y(toRow);
  const bend = Math.min(COLUMN_WIDTH, x2 - x1);
  const k = bend * 0.55;

  switch (t.kind) {
    case "fork":
      return `M ${x1} ${y1} C ${x1 + k} ${y1}, ${x1 + bend - k} ${y2}, ${x1 + bend} ${y2}`;
    case "merge":
      return `M ${x2 - bend} ${y1} C ${x2 - bend + k} ${y1}, ${x2 - k} ${y2}, ${x2} ${y2}`;
    case "cherryPick": {
      const mid = (x2 - x1) / 2;
      return `M ${x1} ${y1} C ${x1 + mid} ${y1}, ${x2 - mid} ${y2}, ${x2} ${y2}`;
    }
  }
}
```

SVG `C` komutu kübik Bezier: iki kontrol noktası + bitiş. Kontrol noktalarını başlangıç ve bitişle **aynı y** üzerine koymak, eğrinin iki uçta da yatay girip çıkmasını sağlıyor — metro makası görüntüsü buradan geliyor. `0.55` çeyrek daireye yakın bir yumuşaklık veriyor.

TypeScript `switch`'in tüm `kind` değerlerini kapsadığını biliyor; Rust tarafına dördüncü bir tür eklersen burada "not all code paths return a value" hatası alırsın.

### 3.4 Renk

```ts
export function transitionColor(t: TransitionDto, lanes: LaneDto[], mode: ColorMode): string {
  if (mode === "kind") return `var(--kind-${t.kind})`;
  const lane = t.kind === "fork" ? lanes[t.toLane] : lanes[t.fromLane];
  return laneColor(lane.color);
}
```

`app.css`'e üç yeni değişken: `--kind-fork`, `--kind-merge`, `--kind-cherryPick` — açık ve koyu tema için ayrı değerlerle.

**Ok başı** için ilk akla gelen SVG'nin `<marker>` elemanı:

```svelte
<marker id="arrow" …><path d="M 0 0 L 8 4 L 0 8 z" fill="context-stroke" /></marker>
```

`fill="context-stroke"` (SVG 2) marker'ın, onu kullanan çizginin rengini almasını sağlar; WebView2'de (Chromium) çalışıyor. Ama uygulamayı Linux'ta WebKitGTK ile denediğimizde ok başları **siyah** çıktı: o motor `context-stroke`'u desteklemiyor. Her renk için ayrı marker tanımlamak yerine ok başını kendi `path`'i olarak çiziyoruz. Merge dönüşü hedefe her zaman yatay girdiği için ok hep sağa bakıyor; hesap basit:

```ts
export function arrowHead(column: number, row: number): string {
  const tip = x(column) - MERGE_RADIUS - 1.5;   // istasyonun halkasının hemen önü
  const cy = y(row);
  return `M ${tip - 7} ${cy - 4} L ${tip} ${cy} L ${tip - 7} ${cy + 4} z`;
}
```

```svelte
{#if t.kind === "merge"}
  <path d={arrowHead(t.toColumn, rowOfLane[t.toLane])} class="arrow"
        fill={transitionColor(t, map.lanes, view.colorMode)} />
{/if}
```

Ders: WebView tabanlı masaüstü uygulamasında "tarayıcı" tek değil. Windows'ta WebView2, macOS'ta WKWebView, Linux'ta WebKitGTK. Yeni bir CSS/SVG özelliği kullanmadan önce üçünü de düşün.

### 3.5 Görünüm tercihleri — `view.svelte.ts`

```ts
class ViewState {
  colorMode = $state<ColorMode>(load().colorMode);
  compact = $state<boolean>(load().compact);

  save() {
    try {
      localStorage.setItem(KEY, JSON.stringify({ colorMode: this.colorMode, compact: this.compact }));
    } catch { /* ignore */ }
  }
}
export const view = new ViewState();
```

Görünüm tercihleri makineye özel ve kaybolursa zararsız; `localStorage` yeterli. Son açılan repolar ise Rust tarafında (Faz 3) çünkü onları uygulama mantığı kullanıyor. Her `localStorage` erişimi `try/catch` içinde: depolama kapalıysa uygulama varsayılanlarla açılır, çökmez.

Toolbar'a iki kontrol eklendi: "Geçiş rengi: Türe göre | Dala göre" düğme grubu ve "Kompakt" onay kutusu.

### 3.6 `MetroView.svelte` güncellemeleri

**Satır seçimi** tek bir türetilmiş fonksiyonla:

```ts
const rowOf = $derived((lane: LaneDto) => (view.compact ? lane.compactRow : lane.row));
const rowOfLane = $derived(map.lanes.map((lane) => rowOf(lane)));
```

Kompakt düğmesine basınca `view.compact` değişiyor → `rowOf` → `rowOfLane` → tüm `y()` hesapları. Tek bir satır kod değişikliği bütün haritayı yeniden diziyor; Svelte'in bağımlılık izlemesi burada parlıyor.

**Katmanlar:** tarih cetveli → raylar → **geçişler** → istasyonlar → işaretler. Geçişler istasyonların altında; eğriler istasyonların beyaz halkasının arkasından geçiyor.

**Hover:** 2px'lik bir çizginin üzerine fareyle gelmek zor. Her geçiş için aynı `d` ile görünmez, 12px kalınlığında bir "ikiz" çiziyoruz:

```svelte
<path {d} class="transition {t.kind}" class:dimmed={focus !== null && hovered !== i} … />
<path {d} class="hit" onpointerenter={() => (hovered = i)} onpointerleave={() => (hovered = null)} />
```

```css
.hit { fill: none; stroke: transparent; stroke-width: 12; pointer-events: stroke; }
```

`pointer-events: stroke` şeffaf çizginin yine de fareyi yakalamasını sağlıyor.

```ts
const focus = $derived.by(() => {
  if (hovered === null) return null;
  const t = map.transitions[hovered];
  return { lanes: new Set([t.fromLane, t.toLane]), columns: new Set([t.fromColumn, t.toColumn]) };
});
```

Raylar, istasyonlar ve etiketler `focus`'ta yoksa `.dimmed` (opaklık %15) alıyor.

**Kompakt modda etiketler:** Soldaki sütun bir satırdaki dalları son aktiviteye göre sıralayıp en fazla üçünü gösteriyor ("+2" gibi taşma notuyla). Ayrıca her rayın başına haritanın içinde küçük bir etiket yazılıyor; kaydırırken hangi parçanın hangi dal olduğu kaybolmuyor.

### 3.7 Lejant — `Legend.svelte`

Sağ alt köşede, haritanın üstünde duran küçük bir kutu. Moda göre içeriği değişiyor: "türe göre"de üç renkli örnek çizgi; "dala göre"de nötr renkte çizgiler ve "yeni dalın rengi / kaynak dalın rengi" açıklaması. Lejanttaki metinler hiçbir zaman çizgi renginde değil — açık sarı ya da açık yeşil yazı zeminde okunmaz. Renk, metnin yanındaki küçük çizgide.

## 4. Takıldığın yerler

| Belirti | Sebep / çözüm |
|---|---|
| Uzun yatay geçişler yanlış renkte | Ray (`trackSpan`) fork'tan sonrasını kapsamıyor; yatay kısım geçişin rengini alıyor. |
| Ok başları siyah | `<marker>` + `context-stroke` kullanılmış; WebKitGTK desteklemiyor. Ok başını ayrı `path` olarak çiz (3.4). |
| Geçişin üzerine gelmek zor | Görünmez `.hit` ikizi yok ya da `pointer-events: stroke` unutulmuş. |
| Kompakt modda iki dal üst üste | `span` aralığı yanlış; core testleri (`overlapping_lanes_never_share_a_compact_row`) bunu yakalamalı. |
| Kompakt düğmesine basınca harita güncellenmiyor | `rowOf` `$derived` değil düz bir fonksiyon; `view.compact`'a bağımlılık izlenmiyor. |

## 5. Ne öğrendik

- Kübik Bezier ile "kısa dönüş, uzun düz yol" metro makası.
- Renklendirmede iki soru (ne oldu / nereden geldi) ve ikincil kodlama (ok, kesikli çizgi).
- SVG `<marker>`/`context-stroke` ve WebView motorları arasındaki farklar.
- Aralık bölümleme ile satır sıkıştırma; closure'ların ödünç alma sınırları.
- Hover için geniş, görünmez isabet alanı; türetilmiş `focus` durumu ile soluklaştırma.
- Görünüm tercihlerini `localStorage`'da güvenli saklamak.

## 6. Kendin dene

1. `trackSpan`'i ön yüzden kaldır, core'un hesapladığı aralığı `LaneDto`'ya `trackFrom`/`trackTo` olarak ekle. Tek kaynak ilkesi burada da geçerli.
2. Kompakt modda silinmiş dalların (`inferred`) kendi satırlarına değil, merge edildikleri dalın hemen altına yerleşmesini dene. Hangi testi yazardın?
3. Lejanttaki bir satırın üzerine gelince o türdeki tüm geçişleri öne çıkar.
