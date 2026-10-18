# java-atlas

Java projelerindeki modül, paket ve dış kütüphane bağımlılıklarını **ülke haritası** gibi çizen, salt okunur, çapraz platform (Windows · Linux · macOS) masaüstü uygulaması.

![atlas-bank haritası](docs/images/faz6-ulke-hover.png)

Tekerlekle yakınlaştıkça ülke ▸ bölge (modül) ▸ şehir (paket) ölçekleri arasında geçilir; çift tıklama o bölgeye uçar, Esc geri döner.

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
VITE_AUTO_OPEN=$PWD/samples/atlas-bank npm run tauri dev   # açılışta örnek projeyi aç
cargo test --workspace   # testler
```

Gereksinimler ve platforma özel notlar: [docs/tutorial/00-kurulum-ve-iskelet.md](docs/tutorial/00-kurulum-ve-iskelet.md)

## Dokümanlar

- [Plan](docs/PLAN.md)
- [Faz 0 — Kurulum ve iskelet](docs/tutorial/00-kurulum-ve-iskelet.md)
- [Faz 1 — Build dosyalarını okumak](docs/tutorial/01-build-dosyalari.md)
- [Faz 2 — tree-sitter ile Java kaynaklarını okumak](docs/tutorial/02-tree-sitter-ile-java.md)
- [Faz 3 — Bağımlılık grafı ve analiz](docs/tutorial/03-graf-ve-analiz.md)
- [Faz 4 — Tauri köprüsü](docs/tutorial/04-tauri-kopru.md)
- [Faz 5 — Harita yerleşimi ve ilk çizim](docs/tutorial/05-treemap-ve-cizim.md)
- [Faz 6 — Yollar ve semantik zoom](docs/tutorial/06-yollar-ve-zoom.md)

Build dosyalarından ne okunduğunu görmek için:

```bash
cargo run -p java-atlas-scan --example inspect -- samples/atlas-bank
# kaynak dosyalarla birlikte:
cargo run -p java-atlas-scan --example inspect -- samples/atlas-bank --sources
# çözümlenmiş graf: döngüler, JPMS ihlalleri, bulgular, metrikler
cargo run -p java-atlas-scan --example inspect -- samples/atlas-bank --atlas
```
