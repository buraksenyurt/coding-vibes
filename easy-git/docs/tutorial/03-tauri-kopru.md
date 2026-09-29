# Faz 3 — Tauri Köprüsü ve Repo Seçimi

> **Önceki:** [02 — Şerit algoritması](02-serit-algoritmasi.md) · **Sonraki:** [04 — SVG ile çizim](04-svg-ile-cizim.md)

## 1. Bu bölümde ne yapacağız

Rust'ta hazır olan `MetroMap`'i ön yüze taşıyacağız:

- Üç **Tauri command'ı**: `open_repository`, `get_metro_map`, `recent_repositories`
- Açık repo'yu tutan paylaşılan **state**
- IPC'den geçen **DTO**'lar ve `ts-rs` ile otomatik üretilen **TypeScript tipleri**
- **Klasör seçici** (`tauri-plugin-dialog`) ve **son açılanlar** listesi (`tauri-plugin-store`)
- Tauri 2'nin **capability** güvenlik modeli

Bölümün sonunda "Repo seç" butonu çalışıyor, seçilen repo'nun özeti ekranda görünüyor. Ayrıca ön yüz, Tauri olmadan tarayıcıda **demo modunda** da açılabiliyor.

## 2. Neden böyle

### IPC: iki dünya arasında JSON

WebView'daki JavaScript, Rust fonksiyonunu doğrudan çağıramaz. `invoke("get_metro_map")` dediğinde Tauri mesajı Rust'a iletir, dönen değeri **JSON'a serileştirip** geri yollar. Bu yüzden sınırı geçen her tip `serde::Serialize` olmak zorunda.

### Neden core tiplerini doğrudan göndermiyoruz?

| Core'da | Ön yüzün istediği | Sebep |
|---|---|---|
| `CommitId([u8; 20])` | `"a3f9c21…"` string | JS'te 20 sayılık dizi işe yaramaz |
| `LaneLabel::Inferred(String)` | `{ label, labelKind: "inferred" }` | TS'te düz alan + string etiket daha rahat |
| `i64` saniye | `number` | `ts-rs` `i64`'ü `bigint` yapar; JSON'da zaten number gelir |
| `snake_case` alanlar | `camelCase` | JS geleneği |

Ayrıca core'a `serde` bağımlılığı eklemek istemiyoruz. DockerCity'deki DTO ↔ Domain ayrımının ters yönü: orada DTO dışarıdan içeri giriyordu, burada içeriden dışarı çıkıyor. Dönüşüm `impl From<&MetroMap> for MetroMapDto` ile tek bir yerde.

### Neden `ts-rs`?

DTO'yu Rust'ta değiştirip TypeScript tarafını unutmak, ancak çalışma zamanında `undefined` olarak fark edilen bir hatadır. `ts-rs` her `#[ts(export)]` tipi için bir `.ts` dosyası üretir; tipler **tek kaynaktan** gelir. Bir alan adını değiştirirsen `npm run check` hemen kırılır.

### State ve thread'ler

Tauri command'ları bir thread havuzunda çalışır. Açık repo'yu tutan yapı **`Send + Sync`** olmalı:

```rust
pub struct AppState {
    current: Mutex<Option<Arc<GixSource>>>,
}
```

- `Mutex` sadece "hangi repo açık" bilgisini değiştirirken kilitlenir.
- Asıl iş (geçmişi yürümek) `Arc`'ın bir kopyası üzerinde, **kilit bırakıldıktan sonra** yapılır. Böylece yavaş bir repo diğer command'ları bekletmez.
- Faz 1'de `GixSource` içine `ThreadSafeRepository` koymamızın sebebi tam olarak bu an.

### async + `spawn_blocking`

gix senkron bir kütüphane. Büyük bir repo'da `build_metro_map` birkaç yüz milisaniye sürebilir. Bunu async command içinde doğrudan çalıştırırsak async runtime'ın thread'ini bloke ederiz. `spawn_blocking` işi ayrı bir thread'e taşır, command `await` ile sonucu bekler — pencere akıcı kalır.

### Capability: "varsayılan olarak yasak"

Tauri 2'de ön yüz, `capabilities/*.json` içinde açıkça izin verilmeyen hiçbir command'ı ya da eklenti fonksiyonunu çağıramaz. Kendi command'larımız otomatik olarak izinli; eklentilerinkini tek tek açıyoruz. Store eklentisini **sadece Rust tarafından** kullandığımız için ön yüze store izni vermiyoruz — gereken en az yetki.

## 3. Adım adım uygulama

### 3.1 Bağımlılıklar

`src-tauri/Cargo.toml`:

```toml
[dependencies]
tauri-plugin-dialog = "2.8"
tauri-plugin-store = "2.5"
thiserror.workspace = true
ts-rs = "12"

[dev-dependencies]
tempfile = "3"
```

```powershell
npm install @tauri-apps/plugin-dialog
```

> Tauri eklentileri Rust **1.90** istiyor. Workspace'teki `rust-version` değerini buna göre güncelledik.

### 3.2 DTO'lar — `src-tauri/src/dto.rs`

```rust
#[derive(Debug, Clone, Serialize, TS)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
pub struct CommitDto {
    pub id: String,
    pub short_id: String,
    pub summary: String,
    pub author_name: String,
    pub author_email: String,
    #[ts(type = "number")]
    pub time: i64,
    pub parents: Vec<String>,
    pub lane: usize,
    pub is_merge: bool,
}

#[derive(Debug, Clone, Serialize, TS)]
#[serde(tag = "kind", rename_all = "camelCase")]
#[ts(export)]
pub enum HeadDto {
    Branch { name: String, target: String },
    Detached { target: String },
    Unborn { name: String },
}
```

`#[serde(tag = "kind")]` Rust enum'unu TypeScript'in en sevdiği şekle, **discriminated union**'a çeviriyor. `ts-rs`'in ürettiği dosya:

```ts
export type HeadDto =
  | { "kind": "branch", name: string, target: string }
  | { "kind": "detached", target: string }
  | { "kind": "unborn", name: string };
```

TS'te `switch (head.kind)` yazdığında her dalda doğru alanlar otomatik tamamlanır. Rust'taki `match`'in güvencesi sınırın öbür tarafına da taşınmış oluyor.

`MetroMapDto` üst yapı: `commits`, `lanes`, `transitions`, `refs`, `head`, `truncated`. Dönüşüm:

```rust
impl From<&MetroMap> for MetroMapDto {
    fn from(map: &MetroMap) -> Self {
        let commits = map.commits.iter().zip(&map.lane_of).map(|(c, &lane)| CommitDto {
            id: c.id.to_hex(),
            short_id: c.id.short(),
            // ...
            lane,
            is_merge: c.is_merge(),
        }).collect();
        // lanes, transitions, refs ...
    }
}
```

`zip` iki paralel vektörü (commit'ler ve şeritleri) birlikte gezmenin deyimsel yolu — Faz 2'deki arena yapısının doğal devamı.

### 3.3 TypeScript tiplerini üretmek

`ts-rs` dosyaları nereye yazacağını `TS_RS_EXPORT_DIR` ortam değişkeninden öğrenir. Bunu workspace köküne bir kez yazıyoruz — `.cargo/config.toml`:

```toml
[env]
TS_RS_EXPORT_DIR = { value = "src/lib/bindings", relative = true }
```

Üretim `cargo test` sırasında olur (`ts-rs` her tip için gizli bir test yazar):

```powershell
cargo test -p easy-git
```

`src/lib/bindings/` altındaki dosyalar repoya commit'lenir; böylece ön yüzü derlemek için Rust testlerini çalıştırmak gerekmez.

### 3.4 Hata tipi

```rust
#[derive(Debug, Clone, Serialize, TS, thiserror::Error)]
#[serde(rename_all = "camelCase")]
#[ts(export)]
#[error("{message}")]
pub struct AppError {
    pub kind: ErrorKind,
    pub message: String,
}

pub enum ErrorKind { NotARepository, NoRepositoryOpen, ReadFailed, Internal }
```

Command `Result<T, AppError>` döndüğünde Tauri `Err` tarafını da serileştirir; JS'te `invoke` bu nesneyle **reject** olur. `RepoError`, `SourceError` ve `tauri::Error` için `From` impl'leri sayesinde command içinde `?` her şeyi `AppError`'a çevirir.

### 3.5 Command'lar — `src-tauri/src/commands.rs`

```rust
#[tauri::command]
pub async fn open_repository(path: String, app: AppHandle, state: State<'_, AppState>)
    -> Result<RepoSummary, AppError>
{
    let source = tauri::async_runtime::spawn_blocking(move || GixSource::open(PathBuf::from(path)))
        .await
        .map_err(|e| AppError::new(ErrorKind::Internal, e.to_string()))??;

    let head = source.head()?;
    let summary = RepoSummary {
        name: source.name(),
        path: source.root().display().to_string(),
        head: HeadDto::from(&head),
    };
    state.set(source);
    recent::remember(&app, RecentRepository { name: summary.name.clone(), path: summary.path.clone() });
    Ok(summary)
}
```

Çift `??`'ye dikkat: ilk `?` thread'in kendisinin çökmesini (`JoinError`), ikincisi `GixSource::open`'ın döndüğü `RepoError`'ı ele alıyor.

```rust
#[tauri::command]
pub async fn get_metro_map(limit: Option<usize>, state: State<'_, AppState>)
    -> Result<MetroMapDto, AppError>
{
    let source = state.get()
        .ok_or_else(|| AppError::new(ErrorKind::NoRepositoryOpen, "no repository is open"))?;
    let options = MapOptions { limit: limit.unwrap_or(MapOptions::default().limit), ..MapOptions::default() };

    tauri::async_runtime::spawn_blocking(move || {
        let map = build_metro_map(source.as_ref(), &options)?;
        Ok(MetroMapDto::from(&map))
    })
    .await
    .map_err(|e| AppError::new(ErrorKind::Internal, e.to_string()))?
}
```

- `State<'_, AppState>` async command'da ödünç alınan bir referans; bu yüzden dönüş tipi mutlaka `Result` olmalı (Tauri'nin bilinen bir kısıtı).
- `source` bir `Arc<GixSource>`; `move` ile closure'a taşınıyor, `as_ref()` ile `&GixSource` olarak algoritmaya veriliyor.

Kayıt — `src-tauri/src/lib.rs`:

```rust
tauri::Builder::default()
    .plugin(tauri_plugin_dialog::init())
    .plugin(tauri_plugin_store::Builder::new().build())
    .manage(AppState::default())
    .invoke_handler(tauri::generate_handler![
        commands::open_repository,
        commands::get_metro_map,
        commands::recent_repositories,
    ])
```

### 3.6 Son açılanlar — `src-tauri/src/recent.rs`

```rust
pub fn remember<R: Runtime>(app: &AppHandle<R>, entry: RecentRepository) {
    let list = push_front(load(app), entry);
    if let Ok(store) = app.store(STORE_FILE) {
        store.set(KEY, serde_json::to_value(list).unwrap_or_default());
        let _ = store.save();
    }
}

fn push_front(mut list: Vec<RecentRepository>, entry: RecentRepository) -> Vec<RecentRepository> {
    list.retain(|r| !r.path.eq_ignore_ascii_case(&entry.path));
    list.insert(0, entry);
    list.truncate(MAX_RECENT);
    list
}
```

Liste mantığı (`push_front`) Tauri'den bağımsız saf bir fonksiyon — kendi testleri var. `eq_ignore_ascii_case` Windows'ta `C:\Repo` ile `c:\repo`'nun aynı klasör olmasını hesaba katıyor. Dosya `%APPDATA%\dev.buraksenyurt.easygit\easy-git.json` altına düşer.

### 3.7 Capability

`src-tauri/capabilities/default.json`:

```json
{
  "identifier": "default",
  "windows": ["main"],
  "permissions": ["core:default", "dialog:allow-open"]
}
```

Sadece "klasör/dosya aç" diyaloğu. Kaydet diyaloğu, mesaj kutusu, store'a doğrudan erişim — hiçbiri yok.

### 3.8 Ön yüz: tek kapı `api.ts`

```ts
export const inTauri = (): boolean => "__TAURI_INTERNALS__" in window;

async function call<T>(command: string, args?: Record<string, unknown>): Promise<T> {
  try {
    return await invoke<T>(command, args);
  } catch (raw) {
    const err = raw as Partial<AppError>;
    throw new Error(err?.message ?? String(raw));
  }
}

export async function pickFolder(): Promise<string | null> {
  if (!inTauri()) return DEMO_PATH;
  const selected = await open({ directory: true, multiple: false, title: "Bir git repository'si seç" });
  return typeof selected === "string" ? selected : null;
}

export async function getMetroMap(limit?: number): Promise<MetroMapDto> {
  if (!inTauri()) return demoMap();
  return call<MetroMapDto>("get_metro_map", { limit });
}
```

Bileşenler `invoke`'u hiç görmüyor. Bunun iki faydası var: IPC yüzeyi tek dosyada okunuyor ve **demo modu** mümkün oluyor.

**Demo modu:** Tauri dışında (`npm run dev` + tarayıcı) fonksiyonlar `src/lib/demo/sample-map.json` dosyasını döndürüyor. Bu dosya fixture repo'dan üretiliyor:

```powershell
cargo test -p easy-git -- --ignored export_demo_map
```

Böylece Faz 4–6'daki görsel işlerde her değişiklikte Rust'ı derlemeden, tarayıcıda hot reload ile çalışabiliyoruz.

### 3.9 Ön yüz state'i — Svelte 5 rune'ları

`src/lib/repo.svelte.ts`:

```ts
class RepoState {
  summary = $state<RepoSummary | null>(null);
  map = $state<MetroMapDto | null>(null);
  recent = $state<RecentRepository[]>([]);
  loading = $state(false);
  error = $state<string | null>(null);

  async open(path: string) {
    this.loading = true;
    this.error = null;
    try {
      this.summary = await api.openRepository(path);
      this.map = await api.getMetroMap();
      await this.loadRecent();
    } catch (e) {
      this.error = e instanceof Error ? e.message : String(e);
    } finally {
      this.loading = false;
    }
  }
}

export const repo = new RepoState();
```

Dosya uzantısı `.svelte.ts` — rune'lar (`$state`) sadece bu uzantılı dosyalarda ve bileşenlerde çalışır. Svelte 4'teki `writable` store'lara, `subscribe`'a, `$` önekine gerek kalmadı; sınıf alanı reaktif.

`Toolbar.svelte` "Repo seç" butonu, son açılanlar menüsü ve `HEAD` bilgisini gösteriyor. `HEAD` etiketi `$derived.by` ile türetiliyor ve `switch (head.kind)` ile discriminated union'dan faydalanıyor.

### 3.10 Çalıştır

```powershell
npm run tauri dev
```

"Repo seç" → herhangi bir repo klasörü → "N commit · M hat · K geçiş" özeti. Uygulamayı kapatıp açınca ▾ menüsünde son açılanlar duruyor.

Sadece ön yüz için:

```powershell
npm run dev        # http://localhost:1420 — demo verisiyle
npm run check      # svelte-check + TypeScript
```

## 4. Takıldığın yerler

| Belirti | Sebep / çözüm |
|---|---|
| `invoke` → `Command open_repository not found` | `generate_handler!` listesine eklenmemiş. |
| Diyalog açılmıyor, konsolda `dialog.open not allowed` | Capability'de `dialog:allow-open` yok. |
| Svelte'te `$state is not defined` | Dosya `.ts`; `.svelte.ts` olmalı. |
| `ts-rs` dosyaları proje dışına yazıldı | `.cargo/config.toml` yok ya da `relative = true` unutulmuş. |
| Async command'da `State` ile ilgili lifetime hatası | Dönüş tipi `Result<…>` olmalı. |
| `requires Rust 1.90` uyarısı | `rustup update`; `rust-version` alanını da güncelle. |

## 5. Ne öğrendik

- Tauri command'ları, `State`, `AppHandle`, `generate_handler!`.
- `Mutex<Option<Arc<T>>>` kalıbı: kısa kilit, uzun iş kilidin dışında.
- `spawn_blocking` ile senkron işi async dünyadan ayırmak.
- DTO katmanı, `serde(tag)` ile discriminated union, `ts-rs` ile tek kaynaktan tip üretimi.
- Tauri 2 capability modeli ve en az yetki.
- Svelte 5 rune'larıyla sınıf tabanlı state.

## 6. Kendin dene

1. `ErrorKind::NotARepository` hatasında ön yüzde "Bu klasör bir git repository'si değil" gibi Türkçe, özel bir mesaj göster (ipucu: `call` fonksiyonu `kind`'ı da taşıyabilir).
2. Son açılanlar menüsüne "listeyi temizle" seçeneği ekle — yeni bir command gerekir.
3. `dialog:allow-open` iznini capability'den geçici olarak sil ve konsoldaki hatayı gözle.
