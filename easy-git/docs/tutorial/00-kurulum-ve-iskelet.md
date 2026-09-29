# Faz 0 — Atölye Kurulumu ve İskelet

> **Önceki:** [PLAN.md](../PLAN.md) · **Sonraki:** [01 — gix ile git okuma](01-gix-ile-git-okuma.md)

## 1. Bu bölümde ne yapacağız

Araç zincirini kurup easy-git'in iskeletini ayağa kaldıracağız: kökte bir **Cargo workspace**, içinde iki kütüphane crate'i (`easy-git-core`, `easy-git-repo`) ve Tauri uygulaması (`src-tauri`); yanında **Svelte 5 + TypeScript + Vite** ile yazılmış ön yüz. Bölümün sonunda `npm run tauri dev` komutu boş ama bizim olan bir pencere açacak.

## 2. Neden böyle

### Tauri'nin iki süreçli yapısı

Tauri uygulaması iki dünyadan oluşur:

| Taraf | Ne çalışır | easy-git'teki görevi |
|---|---|---|
| **Core süreci** | Derlenmiş Rust kodu | Repo'yu okumak, şeritleri hesaplamak |
| **WebView** | HTML/CSS/JS (Windows'ta WebView2) | Metro haritasını çizmek, etkileşim |

İkisi IPC (mesajlaşma) ile konuşur. Electron'dan farkı: tarayıcı motoru uygulamayla paketlenmez, işletim sisteminin WebView'ı kullanılır. Sonuç birkaç MB'lık bir `.exe`.

### Neden `create-tauri-app` çıktısıyla yetinmiyoruz?

Şablon tek bir `src-tauri` crate'i üretir. Biz iş mantığını Tauri'den **fiziksel olarak** ayırmak istiyoruz; bu yüzden kökte bir workspace kurup `src-tauri`'yi onun bir üyesi yapıyoruz:

```
easy-git/
├── Cargo.toml              ← workspace kökü
├── crates/
│   ├── easy-git-core/      ← domain + şerit algoritması (gix'ten ve Tauri'den habersiz)
│   └── easy-git-repo/      ← gix adaptörü
├── src-tauri/              ← Tauri uygulaması
├── src/                    ← Svelte ön yüz
├── package.json · vite.config.ts · tsconfig.json · index.html
└── docs/
```

Bağımlılık yönü: `src-tauri → easy-git-repo → easy-git-core`. DockerCity'deki "Domain hiçbir şeye bağımlı değil" kuralının Rust karşılığı; derleyici bunu `Cargo.toml` seviyesinde garanti ediyor. `easy-git-core`'un `[dependencies]` bölümü bilerek boş.

### Tek `target/` klasörü

Workspace'in yan etkisi: tüm crate'ler kökteki tek `target/` klasörüne derlenir. Tauri + gix ilk derlemede epey bağımlılık indirip derler; bunu bir kez yapmak iyi.

## 3. Adım adım uygulama

### 3.1 Araç zinciri (Windows)

| Araç | Kontrol komutu | Not |
|---|---|---|
| Visual Studio Build Tools — *Desktop development with C++* | — | Rust'ın MSVC linker'ı için şart |
| Rust (MSVC toolchain) | `rustc --version` | En az **1.90** (Tauri eklentileri bunu istiyor; edition 2024 için de yeterli) |
| Node.js LTS | `node --version` | 20+ |
| WebView2 | — | Windows 11'de hazır gelir |

```powershell
rustup default stable-x86_64-pc-windows-msvc
rustup update
```

### 3.2 Workspace kökü — `Cargo.toml`

```toml
[workspace]
resolver = "3"
members = ["crates/easy-git-core", "crates/easy-git-repo", "src-tauri"]

[workspace.package]
version = "0.1.0"
edition = "2024"
rust-version = "1.90"
license = "MIT"
authors = ["Burak Selim Senyurt"]

[workspace.dependencies]
easy-git-core = { path = "crates/easy-git-core" }
easy-git-repo = { path = "crates/easy-git-repo" }
serde = { version = "1", features = ["derive"] }
serde_json = "1"
thiserror = "2"

[profile.dev.package."*"]
opt-level = 1
```

- **`[workspace.package]`**: sürüm, edition gibi ortak alanlar tek yerde. Üye crate'ler `version.workspace = true` diyerek devralır.
- **`[workspace.dependencies]`**: ortak bağımlılıkların sürümü tek yerde. Üyeler `serde.workspace = true` der.
- **`profile.dev.package."*"`**: *bağımlılıkları* debug modda bile hafif optimize eder. Kendi kodumuz hızlı derlenmeye devam eder, ama gix binlerce nesne gezerken uygulama sürünmez.

### 3.3 Kütüphane crate'leri

`crates/easy-git-core/Cargo.toml`:

```toml
[package]
name = "easy-git-core"
description = "Domain model and lane layout for easy-git. Knows nothing about gix or Tauri."
version.workspace = true
edition.workspace = true
rust-version.workspace = true
license.workspace = true

[dependencies]
```

`crates/easy-git-core/src/lib.rs`:

```rust
//! easy-git-core: the domain of easy-git.
//!
//! This crate must stay free of `gix` and `tauri`. It receives history through
//! a trait and turns it into a metro map.

/// Name of the product, shared by every layer.
pub const PRODUCT_NAME: &str = "easy-git";
```

`easy-git-repo` aynı kalıpta; tek farkı `easy-git-core.workspace = true` bağımlılığı.

### 3.4 Tauri crate'i — `src-tauri/`

`src-tauri/Cargo.toml`:

```toml
[package]
name = "easy-git"
# ... workspace alanları

[lib]
name = "easy_git_lib"
crate-type = ["staticlib", "cdylib", "rlib"]

[build-dependencies]
tauri-build = { version = "2.7", features = [] }

[dependencies]
easy-git-core.workspace = true
easy-git-repo.workspace = true
tauri = { version = "2.12", features = [] }
serde.workspace = true
serde_json.workspace = true
```

Uygulama mantığı `lib.rs`'te, `main.rs` sadece onu çağırıyor. Bu Tauri 2'nin önerdiği düzen — aynı kod mobil hedeflerde de giriş noktası olabiliyor.

`src-tauri/src/main.rs`:

```rust
// Prevents an extra console window on Windows in release builds. Do not remove.
#![cfg_attr(not(debug_assertions), windows_subsystem = "windows")]

fn main() {
    easy_git_lib::run()
}
```

`src-tauri/src/lib.rs`:

```rust
#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .run(tauri::generate_context!())
        .expect("error while running easy-git");
}
```

`generate_context!()` makrosu derleme zamanında `tauri.conf.json`'u, ikonları ve capability dosyalarını okuyup binary'ye gömer. `build.rs` içindeki `tauri_build::build()` de bu hazırlığın yapıldığı yer.

`src-tauri/tauri.conf.json` — önemli alanlar:

```json
{
  "productName": "easy-git",
  "identifier": "dev.buraksenyurt.easygit",
  "build": {
    "beforeDevCommand": "npm run dev",
    "devUrl": "http://localhost:1420",
    "beforeBuildCommand": "npm run build",
    "frontendDist": "../dist"
  },
  "app": {
    "windows": [{ "title": "easy-git", "width": 1280, "height": 800, "minWidth": 900, "minHeight": 560 }]
  },
  "bundle": { "active": true, "targets": ["msi", "nsis"], "icon": ["icons/32x32.png", "icons/icon.ico", "..."] }
}
```

- Geliştirmede WebView, Vite'ın `1420` portundaki sunucusuna bağlanır (hot reload).
- Release'de `dist/` klasörü binary'ye gömülür.

`src-tauri/capabilities/default.json`:

```json
{
  "identifier": "default",
  "windows": ["main"],
  "permissions": ["core:default"]
}
```

Tauri 2'de ön yüz **varsayılan olarak hiçbir şeye erişemez**; neye izin verildiyse o. Şimdilik sadece çekirdek izinler. Faz 3'te klasör seçici için buraya ekleme yapacağız.

### 3.5 İkon

`tauri-build`, Windows'ta `icons/icon.ico` bulamazsa derlemeyi durdurur. 1024×1024 bir PNG'den tüm boyutları üretmek tek komut:

```powershell
npx tauri icon assets/app-icon.png -o src-tauri/icons
```

Android/iOS klasörlerini silebiliriz; hedefimiz sadece Windows.

### 3.6 Ön yüz

`package.json` (özet):

```json
{
  "type": "module",
  "scripts": {
    "dev": "vite",
    "build": "vite build",
    "check": "svelte-check --tsconfig ./tsconfig.json",
    "tauri": "tauri"
  },
  "dependencies": { "@tauri-apps/api": "^2.12.0" },
  "devDependencies": {
    "@sveltejs/vite-plugin-svelte": "^7.0.0",
    "@tauri-apps/cli": "^2.12.0",
    "svelte": "^5.57.0",
    "svelte-check": "^4.3.0",
    "typescript": "^5.9.0",
    "vite": "^8.0.0"
  }
}
```

`vite.config.ts`:

```ts
export default defineConfig({
  plugins: [svelte()],
  clearScreen: false,          // Rust hataları terminalden silinmesin
  server: {
    port: 1420,
    strictPort: true,          // port doluysa başka porta kaçma — Tauri 1420'yi bekliyor
    watch: { ignored: ["**/src-tauri/**", "**/target/**"] },
  },
});
```

`src/main.ts` — Svelte 5'te bileşenler `new App()` ile değil `mount()` ile başlatılır:

```ts
import { mount } from "svelte";
import App from "./App.svelte";
import "./app.css";

export default mount(App, { target: document.getElementById("app")! });
```

`src/app.css` açık/koyu tema için CSS değişkenlerini tanımlıyor (`prefers-color-scheme`). Metro haritasındaki tüm renkler ileride bu değişkenlerden türeyecek.

### 3.7 Çalıştır

```powershell
cd easy-git
npm install
npm run tauri dev
```

İlk derleme birkaç dakika sürer. Sonunda "easy-git — Dalların metro haritası burada görünecek." yazan bir pencere açılır.

Workspace'in bütün olarak derlendiğini doğrulamak için:

```powershell
cargo build --workspace
cargo test --workspace
```

## 4. Takıldığın yerler

| Belirti | Sebep / çözüm |
|---|---|
| `link.exe not found` | Visual Studio Build Tools'ta C++ workload eksik. |
| `icons/icon.ico not found` | 3.5'teki `tauri icon` komutu çalıştırılmamış. |
| Pencere açılıyor ama boş ve beyaz | Vite 1420'de çalışmıyor; `strictPort` sayesinde port çakışması varsa terminalde hata görürsün. |
| `edition 2024 is unstable` | Rust sürümü eski; `rustup update`. |
| İlk derleme çok uzun | Normal. Sonraki derlemeler sadece değişen crate'i derler. |

## 5. Ne öğrendik

- Tauri'nin core süreci + WebView mimarisi ve IPC fikri.
- Cargo workspace: `workspace.package`, `workspace.dependencies`, tek `target/`.
- Bağımlılık yönünü `Cargo.toml` ile zorlamak: core crate'in boş bağımlılık listesi bir tasarım kararı.
- `tauri.conf.json` ve capabilities: Tauri 2'nin "varsayılan olarak yasak" güvenlik modeli.
- Svelte 5'in `mount()` ile başlatılması.

## 6. Kendin dene

1. `tauri.conf.json`'daki pencere başlığını değiştirip `npm run tauri dev` ile farkı gör — Rust tarafı yeniden derleniyor mu, dikkat et.
2. `easy-git-core`'a bilerek `tauri` bağımlılığı ekleyip neden bunun bir mimari ihlal olduğunu kendine açıkla; sonra geri al.
3. `cargo tree -p easy-git-core` ile core crate'in bağımlılık ağacının gerçekten boş olduğunu doğrula.
