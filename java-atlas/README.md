# java-atlas

Java projelerindeki modül, paket ve dış kütüphane bağımlılıklarını **ülke haritası** gibi çizen, salt okunur, çapraz platform (Windows · Linux · macOS) masaüstü uygulaması.

- **Bölge** = build modülü (Maven/Gradle) · **Gümrüklü sınır** = JPMS modülü
- **Şehir** = paket · **Bina** = tip
- **Yollar** = bağımlılıklar · **Adalar** = dış kütüphaneler
- Kırmızı yollar = döngüler ve JPMS ihlalleri

Analiz kaynak kod üzerinden yapılır (tree-sitter); JDK, Maven ya da Gradle kurulu olması gerekmez.

## Yığın

Rust (workspace) · Tauri 2 · tree-sitter-java · quick-xml · Svelte 5 + TypeScript + Vite

```
crates/java-atlas-core   domain, analiz, yerleşim (altyapıdan habersiz)
crates/java-atlas-scan   build dosyaları + Java kaynakları → olgular
src-tauri                Tauri kabuğu
src                      Svelte ön yüz
```

## Çalıştırma

```bash
npm install
npm run tauri dev        # geliştirme
cargo test --workspace   # testler
```

Gereksinimler ve platforma özel notlar: [docs/tutorial/00-kurulum-ve-iskelet.md](docs/tutorial/00-kurulum-ve-iskelet.md)

## Dokümanlar

- [Plan](docs/PLAN.md)
- [Faz 0 — Kurulum ve iskelet](docs/tutorial/00-kurulum-ve-iskelet.md)
- [Faz 1 — Build dosyalarını okumak](docs/tutorial/01-build-dosyalari.md)

Build dosyalarından ne okunduğunu görmek için:

```bash
cargo run -p java-atlas-scan --example inspect -- samples/atlas-bank
```
