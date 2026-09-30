# easy-git

Git dallarını **metro haritası** gibi gösteren, salt okunur bir Windows masaüstü dashboard'u. Her dal soldan sağa akan bir hat, her commit bir istasyon; dalların ayrıldığı ve birleştiği yerler renkli makaslar.

**Rust + Tauri 2 + Svelte 5** · git erişimi **gitoxide (`gix`)** ile · repo'ya hiçbir şey yazmaz.

![Genel görünüm](docs/images/overview.png)

## Neler var (MVP — Faz 0–6)

- Yerel makineden repo seçme, son açılanlar listesi: kayıtlar × ile elle çıkarılabilir; taşınmış ya da silinmiş repolar bilgi verilerek kendiliğinden çıkarılır
- Dallar yatay hatlar halinde; açılışta en güncel commit'ler görünür
- Fork, merge ve cherry-pick geçişleri — **türe göre** ya da **dala göre** renklendirme, lejant
- Silinmiş ama merge edilmiş dalların adı merge mesajından geri bulunur
- Kenar çubuğu: ana dala göre ↑önde ↓geride, merge edildi ✓, bayat dal, remote durumu ☁
- Commit ve geçişlerde tooltip; tıklayınca detay paneli (parent'lar arasında gezinme)
- Dal gizleme/öne çıkarma, kompakt görünüm
- Gece/gündüz modu: Windows temasını izle (varsayılan), ya da araç çubuğundan ☀ / ☾ ile sabitle; seçim hatırlanır

![Koyu tema, geçiş vurgusu](docs/images/dark-kind.png)

## Çalıştırma

Gerekenler: Rust 1.90+ (MSVC), Visual Studio Build Tools (C++), Node.js 20+, WebView2.

```powershell
npm install
npm run tauri dev
```

Denemek için hazır bir örnek repo:

```powershell
cargo run -p easy-git-repo --example make-sample-repo -- C:\temp\easy-git-sample
# uygulamada: Repo seç → C:\temp\easy-git-sample\sample
```

Sadece arayüz üzerinde çalışmak için (Tauri'siz, demo verisiyle): `npm run dev` → http://localhost:1420

Testler:

```powershell
cargo test --workspace
npm run check
```

## Hızlı deneme: git flow örneği

Uzak bir repoya bağlanmadan, git flow modeline göre dallanmış hayali bir repo üretip easy-git'te açabilirsin. Windows'ta **Git Bash** içinden (Git for Windows ile gelir), Linux/macOS'ta herhangi bir terminalden:

```bash
./scripts/git-flow-demo.sh              # ./git-flow-sandbox klasörüne kurar
./scripts/git-flow-demo.sh ~/gf-deneme  # ya da istediğin klasöre
```

Betik son iki aya yayılan, bugün biten bir hikâye yazar; senin git ayarlarına ve kimliğine dokunmaz:

| Dal | Durum | easy-git'te beklenen |
|---|---|---|
| `main`, `develop` | yaşıyor | en üstteki iki hat; `v1.0.0` ve `v1.0.1` tag bayrakları `main`'de |
| `feature/user-auth`, `feature/product-search` | merge edildi, silindi | "silinmiş dal" hatları, adları merge mesajından |
| `feature/shopping-cart` | önce `develop`'u içine aldı, sonra merge edildi, silindi | `develop` → dal yönünde bir merge oku |
| `release/1.0.0`, `hotfix/1.0.1` | `main` ve `develop`'a merge edildi, silindi | aynı daldan iki hedefe iki merge |
| `feature/wishlist` | 7 hafta önce bırakıldı | kenar çubuğunda **bayat** rozeti |
| `feature/payment-gateway`, `release/1.1.0` | devam ediyor | açık uçlu hatlar |

Arayüzü açmadan metin çizimini görmek için:

```bash
cargo run -p easy-git-repo --example describe-repo -- ./git-flow-sandbox
```

## Dağıtım (başka bir Windows makinesi için)

Visual Studio 2022'nin **x64 Native Tools Command Prompt**'u içinden:

```powershell
npm install
npm run tauri build
```

Çıktılar proje kökündeki `target` altında:

| Dosya | Ne zaman |
|---|---|
| `target\release\bundle\nsis\easy-git_<sürüm>_x64-setup.exe` | **Önerilen.** Kullanıcı bazında kurar, yönetici izni istemez. |
| `target\release\bundle\msi\easy-git_<sürüm>_x64_en-US.msi` | Tüm makineye kurulum; yönetici izni ister. |
| `target\release\easy-git.exe` | Kurulumsuz, tek dosya. Ön yüz exe'ye gömülü. |

Hedef makinede Rust, Node, Visual Studio ya da **git gerekmez** (repo gix ile doğrudan okunur). WebView2 Windows 11'de hazır; eksikse kurulum dosyası indirip kurar. C çalışma zamanı `.cargo/config.toml` içindeki `+crt-static` ayarıyla exe'ye gömülü, `VCRUNTIME140.dll` aranmaz.

Bilinen durumlar:

- **"Windows bilgisayarınızı korudu" uyarısı:** kurulum dosyası imzasız. *Ek bilgi → Yine de çalıştır.* Kaldırmak için bir kod imzalama sertifikası `tauri.conf.json` → `bundle.windows` altına eklenir.
- **MSI adımı `light.exe` hatası verirse:** Windows'ta VBScript isteğe bağlı özelliği kapalı olabilir. Açabilir ya da `tauri.conf.json` → `bundle.targets` değerini `["nsis"]` yapabilirsin.
- **İlk `tauri build`** WiX ve NSIS araçlarını internetten indirir.
- **`LNK1104: cannot open file 'msvcrt.lib'`:** Rust, C++ bileşenleri eksik bir Visual Studio kurulumunu (ör. bir Insiders sürümü) seçmiş. Komutları VS 2022 Native Tools Command Prompt'tan çalıştır ya da o kuruluma *Desktop development with C++* workload'unu ekle.

Yeni sürüm çıkarırken `src-tauri\tauri.conf.json` içindeki `version` ile kökteki `Cargo.toml` → `[workspace.package] version` birlikte artırılmalı; kurulum dosyasının adı ve Windows'un yükseltme algılaması bu numaraya bağlı.

## Yapı

```
crates/easy-git-core   domain + şerit algoritması + dal istatistikleri (bağımlılıksız)
crates/easy-git-repo   gix adaptörü + test fixture'ı
src-tauri              Tauri command'ları, state, DTO'lar (ts-rs ile TS tipleri)
src                    Svelte 5 ön yüz
docs                   plan ve tutorial serisi
```

## Tutorial serisi

| Faz | Konu |
|---|---|
| [00](docs/tutorial/00-kurulum-ve-iskelet.md) | Kurulum, Cargo workspace, Tauri + Svelte iskeleti |
| [01](docs/tutorial/01-gix-ile-git-okuma.md) | gix ile ref, HEAD ve geçmiş okuma; deterministik fixture |
| [02](docs/tutorial/02-serit-algoritmasi.md) | Şerit algoritması: commit'i dala atamak, geçişler, snapshot testleri |
| [03](docs/tutorial/03-tauri-kopru.md) | Tauri command'ları, state, DTO + ts-rs, capability'ler |
| [04](docs/tutorial/04-svg-ile-cizim.md) | SVG ile hatlar ve istasyonlar |
| [05](docs/tutorial/05-gecisler-ve-renkler.md) | Geçiş eğrileri, iki renk modu, satır sıkıştırma |
| [06](docs/tutorial/06-etkilesim-ve-paneller.md) | Tooltip, detay paneli, ahead/behind, bayat dal |

Plan ve sonraki fazlar: [docs/PLAN.md](docs/PLAN.md)

## Örnek Çalışma Zamanı Çıktısı

![easy-git-runtime-1](EasyGitRuntime_00.png)

Kompakt görünüm;

![easy-git-runtime-2](EasyGitRuntime_01.png)

Belli bir dal seçimi;

![easy-git-runtime-3](EasyGitRuntime_02.png)

Gece/Gündüz modu eklendikten sonra;

![easy-git-runtime-4](EasyGitRuntime_03.png)
