# easy-git

Git dallarını **metro haritası** gibi gösteren, salt okunur bir Windows masaüstü dashboard'u. Her dal soldan sağa akan bir hat, her commit bir istasyon; dalların ayrıldığı ve birleştiği yerler renkli makaslar.

**Rust + Tauri 2 + Svelte 5** · git erişimi **gitoxide (`gix`)** ile · repo'ya hiçbir şey yazmaz.

![Genel görünüm](docs/images/overview.png)

## Neler var (MVP — Faz 0–6)

- Yerel makineden repo seçme, son açılanlar listesi
- Dallar yatay hatlar halinde; açılışta en güncel commit'ler görünür
- Fork, merge ve cherry-pick geçişleri — **türe göre** ya da **dala göre** renklendirme, lejant
- Silinmiş ama merge edilmiş dalların adı merge mesajından geri bulunur
- Kenar çubuğu: ana dala göre ↑önde ↓geride, merge edildi ✓, bayat dal, remote durumu ☁
- Commit ve geçişlerde tooltip; tıklayınca detay paneli (parent'lar arasında gezinme)
- Dal gizleme/öne çıkarma, kompakt görünüm, açık/koyu tema

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
