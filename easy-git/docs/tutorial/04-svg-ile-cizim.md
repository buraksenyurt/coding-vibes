# Faz 4 — SVG ile İlk Görselleştirme

> **Önceki:** [03 — Tauri köprüsü](03-tauri-kopru.md) · **Sonraki:** [05 — Geçişler ve renkler](05-gecisler-ve-renkler.md)

## 1. Bu bölümde ne yapacağız

`MetroMapDto`'yu ilk kez ekrana çizeceğiz:

- Her dal için yatay bir **hat**
- Her commit için bir **istasyon**; merge commit'ler için çift halkalı **aktarma istasyonu**
- Solda, yatay kaydırırken yerinde kalan **yapışkan dal etiketleri**
- Üstte gün değişimlerini gösteren bir **tarih cetveli**
- `v1.0` gibi tag'ler için **bayrak**, `HEAD` için nabız gibi atan bir **"buradasınız" halkası**
- Açılışta görünümün **en sağa** (en yeni commit'lere) kaydırılması

Hatlar arası geçiş eğrileri bir sonraki fazın konusu. Bu fazın sonunda ekran şöyle:

```
                5 Oca        6 Oca              7 Oca          8 Oca
 main          ●─●───────────────◎─────────────────────◎─●─(●) HEAD
 develop            ●───────◎──────────◎──────◎─●
 release/1.0                                        ●
 hotfix/crash                        ●
 feature/login        ●─●─●
 feature/payments              ●─●
 feature/old-search                         ●─●
   silinmiş dal
```

## 2. Neden böyle

### Neden SVG?

| Seçenek | Artı | Eksi |
|---|---|---|
| **SVG** | Her istasyon bir DOM elemanı: hover, tıklama, CSS ile tema bedava. Svelte ile deklaratif. | On binlerce eleman ağırlaşır |
| Canvas | Çok hızlı | Etkileşimi (hangi daireye tıklandı?) elle hesaplamak gerekir |
| WebGL | Aşırı hızlı | Bu problem için aşırı karmaşık |

1.500 commit ≈ birkaç bin SVG elemanı; tarayıcılar bunu rahat taşır. Performans sınırına gelirsek Faz 8'de sanallaştırma yapacağız.

### Geometriyi bileşenden ayırmak

Rust bize **indeksler** veriyor (sütun, satır). Piksele çevirmek ayrı bir sorumluluk: `geometry.ts` içinde saf fonksiyonlar. Bileşen sadece "neyi nereye koyacağını" söylüyor, "kaç piksel" sorusunu sormuyor. Faz 8'de zoom eklediğimizde değişecek tek yer bu dosya olacak.

### Renk: doğrulanmış bir palet

Renkleri gözle seçmedik. Kategorik bir palet — **8 renk, sabit sıra**, açık ve koyu tema için ayrı adımlar — renk körlüğü simülasyonunda bitişik renklerin ayırt edilebildiği doğrulanarak seçildi. İki kural:

1. **`main`/`master` her zaman ilk renk (mavi).** Renk sıraya göre değil kimliğe göre verilir; başka bir repo açtığında `main` yine mavi.
2. **Renk tek başına kimlik taşımaz.** Her hattın adı solda her zaman görünür; renk sadece etiketle hattı eşleştirmeye yardım eder. Bu yüzden 8'den fazla dal olduğunda renklerin tekrar etmesi sorun değil.

Plan 10 renk öngörüyordu; doğrulamadan geçen palet 8 renk olduğu için core'daki `PALETTE_SIZE` sabitini 8'e çektik.

Renkler CSS değişkeni olarak `app.css`'te: `--lane-0 … --lane-7`. Koyu tema bloğu aynı değişkenleri koyu zemine uygun adımlarla yeniden tanımlıyor. SVG'de `stroke="var(--lane-2)"` yazmak yeterli; tema değişince her şey kendiliğinden güncelleniyor.

## 3. Adım adım uygulama

### 3.1 `src/lib/metro/geometry.ts`

```ts
export const COLUMN_WIDTH = 28;
export const ROW_HEIGHT = 40;
export const HEADER_HEIGHT = 32;   // tarih cetveli
export const PADDING_X = 24;
export const LABEL_WIDTH = 200;

export const x = (column: number): number => PADDING_X + column * COLUMN_WIDTH;
export const y = (row: number): number => HEADER_HEIGHT + row * ROW_HEIGHT + ROW_HEIGHT / 2;

export const laneColor = (color: number): string => `var(--lane-${color % 8})`;
```

Tarih cetveli için gün değişimlerini bulan fonksiyon:

```ts
export function dateTicks(map: MetroMapDto, minSpacing = 88): DateTick[] {
  const fmt = new Intl.DateTimeFormat("tr-TR", { day: "numeric", month: "short" });
  const ticks: DateTick[] = [];
  let lastDay = "";
  let lastX = -Infinity;
  map.commits.forEach((commit, column) => {
    const day = new Date(commit.time * 1000).toDateString();
    if (day === lastDay) return;
    lastDay = day;
    if (x(column) - lastX < minSpacing) return;   // üst üste binecekse atla
    lastX = x(column);
    ticks.push({ column, label: fmt.format(new Date(commit.time * 1000)) });
  });
  return ticks;
}
```

X ekseni tarih değil topoloji olduğu için (Faz 2) günler eşit aralıklı değil; cetvel sadece "bu bölge hangi güne denk geliyor" hissi veriyor. `Intl.DateTimeFormat("tr-TR")` "5 Oca" gibi yerel kısaltmaları hazır veriyor.

### 3.2 `MetroView.svelte` — iskelet

```svelte
<script lang="ts">
  let { map }: { map: MetroMapDto } = $props();
  let scroller: HTMLDivElement | undefined = $state();

  const size = $derived(canvasSize(map));
  const ticks = $derived(dateTicks(map));
  const rowOfLane = $derived(map.lanes.map((lane) => lane.row));
  const columnOf = $derived(new Map(map.commits.map((c, i) => [c.id, i])));
  const headColumn = $derived(map.head.kind === "unborn" ? undefined : columnOf.get(map.head.target));
</script>
```

- `$props()` Svelte 5'te prop'ları almanın yolu; tip tanımı doğrudan destructuring üzerinde.
- `$derived` ile türetilen her değer `map` değiştiğinde yeniden hesaplanıyor. `columnOf` gibi bir `Map`'i her istasyon için tekrar tekrar aramak yerine bir kez kuruyoruz.
- `headColumn` satırında `map.head.kind === "unborn"` kontrolünden sonra TypeScript `map.head.target`'ın var olduğunu biliyor — Faz 3'teki discriminated union'ın meyvesi.

### 3.3 Yapışkan etiketler

Yatay kaydırılabilen tek bir kutu, içinde yan yana iki çocuk: etiket sütunu ve SVG.

```svelte
<div class="scroller" bind:this={scroller}>
  <div class="canvas" style:height="{size.height}px">
    <div class="labels" style:width="{LABEL_WIDTH}px"> … </div>
    <svg width={size.width} height={size.height}> … </svg>
  </div>
</div>
```

```css
.scroller { position: absolute; inset: 0; overflow: auto; }
.canvas   { display: flex; width: max-content; min-width: 100%; }
.labels   { position: sticky; left: 0; z-index: 2; background: var(--surface); }
```

`position: sticky; left: 0` ile etiket sütunu yatay kaydırmada solda kalıyor ama dikey kaydırmada SVG ile birlikte hareket ediyor. İki ayrı kaydırma kutusunu JavaScript ile senkronlamaya gerek yok. `background` şart: yoksa kaydırılan hatlar etiketlerin arkasından görünür.

Silinmiş dallar (`labelKind: "inferred"`) italik, soluk ve altında küçük "silinmiş dal" notuyla gösteriliyor.

### 3.4 Katmanlar

SVG'de sonra çizilen üstte görünür. Sıra:

```svelte
<g class="ruler">    <!-- 1. tarih çizgileri -->
<g class="tracks">   <!-- 2. hatlar -->
<!-- (Faz 5: geçiş eğrileri buraya) -->
<g class="stations"> <!-- 3. istasyonlar -->
<g class="markers">  <!-- 4. tag bayrakları ve HEAD -->
```

**Hat:** şeridin ilk ve son commit'i arasında düz bir çizgi.

```svelte
{#each map.lanes as lane (lane.id)}
  <line x1={x(lane.firstColumn)} x2={x(lane.lastColumn)}
        y1={y(lane.row)} y2={y(lane.row)}
        stroke={laneColor(lane.color)} />
{/each}
```

`(lane.id)` bir **keyed each**: harita yenilendiğinde Svelte elemanları kimliklerine göre eşleştirir, gereksiz DOM yeniden yaratmaz.

**İstasyon:**

```svelte
{#each map.commits as commit, column (commit.id)}
  {@const lane = map.lanes[commit.lane]}
  {@const cx = x(column)}
  {@const cy = y(rowOfLane[commit.lane])}
  {#if commit.isMerge}
    <circle {cx} {cy} r={MERGE_RADIUS} class="merge-outer" stroke={laneColor(lane.color)} />
    <circle {cx} {cy} r={MERGE_RADIUS - 3.5} fill={laneColor(lane.color)} class="dot" />
  {:else}
    <circle {cx} {cy} r={STATION_RADIUS} fill={laneColor(lane.color)} class="dot" />
  {/if}
{/each}
```

`{@const}` blok içinde yerel değişken tanımlıyor. `.dot`'un zemin renginde 2px'lik kenarlığı (`stroke: var(--surface)`) istasyonu hattın üzerinde "kesik" gibi gösteriyor — metro haritalarındaki beyaz halkalı durakların etkisi, ek bir çizim olmadan.

**Tag bayrağı ve HEAD:** `refs` içinden `kind === "tag"` olanlar için bir direk + küçük dikdörtgen. HEAD için dış halka ve `@keyframes pulse` animasyonu. `prefers-reduced-motion` açık olan kullanıcıda animasyon kapanıyor.

### 3.5 En sağa kaydırma

```ts
$effect(() => {
  void map;
  tick().then(() => {
    if (scroller) scroller.scrollLeft = scroller.scrollWidth;
  });
});
```

- `$effect` içinde okunan her reaktif değer bağımlılık olur. `void map;` "bu efekt `map`'e bağlı" demenin açık yolu.
- `tick()` Svelte'in DOM'u güncellemesini bekler; yoksa eski genişliğe göre kaydırırdık.

### 3.6 `App.svelte`

Faz 3'teki özet metni yerine:

```svelte
{:else if repo.map}
  <MetroView map={repo.map} />
```

### 3.7 Dene

```powershell
npm run dev          # tarayıcıda demo verisiyle — Rust derlemeden anında
npm run tauri dev    # gerçek repo ile
```

## 4. Takıldığın yerler

| Belirti | Sebep / çözüm |
|---|---|
| Etiketler kaydırınca kayboluyor | `.canvas` `display: flex` değil ya da `.labels`'ın arka planı yok. |
| Hatlar istasyonların üstünde | Katman sırası; `<g class="tracks">` istasyonlardan önce gelmeli. |
| Açılışta sola dayalı | `tick()` beklenmeden kaydırılmış; ya da `scroller` henüz bağlanmamış. |
| Koyu temada renkler çok parlak | Koyu tema için ayrı adımlar tanımlanmamış; aynı hex iki zeminde farklı görünür. |
| `$state is not defined` | Rune'lar sadece `.svelte` ve `.svelte.ts` dosyalarında çalışır. |

## 5. Ne öğrendik

- SVG'nin bu ölçekte neden doğru araç olduğu; katman sırası.
- Geometriyi saf fonksiyonlara ayırmak.
- `position: sticky` ile iki eksenli kaydırmada yapışkan sütun.
- Svelte 5: `$props`, `$derived`, `$effect`, `{@const}`, keyed `{#each}`, `tick()`.
- Renkleri CSS değişkenleriyle yönetmek; doğrulanmış palet ve "renk kimliği tek başına taşımaz" ilkesi.

## 6. Kendin dene

1. `COLUMN_WIDTH`'i 16'ya düşür. Hangi eleman önce okunmaz hale geliyor?
2. Her dalın ucuna (`lastColumn`) küçük bir metin etiketi ekle. 20 dallı bir repoda ne oluyor?
3. `dateTicks`'e hafta sonlarını hafif gölgeleyen bir arka plan şeridi ekle.
