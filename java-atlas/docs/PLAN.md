# java-atlas — Proje Plan Dokümanı

> **Atölye:** Fikirler Atölyesi
> **Doküman sürümü:** v1.6 — 19 Ekim 2026
> **Dokümanlar:** bu klasör (`java-atlas/docs`)
> **Kod:** `coding-vibes/java-atlas`
> **Durum:** Faz 0–6 tamamlandı — sıradaki: Faz 7 (MVP'nin son fazı)

---

## 1. Tek Cümlelik Tanım

Diskteki bir Java projesini (Maven/Gradle, tek ya da çok modüllü, JPMS'li ya da değil) **derlemeden** okuyup modüller, paketler ve dış kütüphaneler arasındaki bağımlılıkları **bir ülke haritası** gibi çizen; Windows, Linux ve macOS'ta çalışan salt okunur bir masaüstü dashboard'u.

## 2. Atölyenin Amacı

| Katman | Hedef |
|---|---|
| **Ürün** | "Bu proje neye bağımlı, kim kime dokunuyor, döngü nerede, hangi modül sınırı deliniyor?" sorularına tek bakışta cevap veren bir harita. jdeps/JDepend çıktısını okumak yerine gezilebilen bir atlas. |
| **Öğreti** | Rust (workspace, trait, enum, hata yönetimi, arena graf, paralellik), **tree-sitter** ile gerçek bir dilin sözdizimi ağacını gezmek, `quick-xml` ile akış tabanlı XML okuma, graf algoritmaları (Tarjan SCC), yerleşim algoritmaları (squarified treemap), Tauri 2 (command, state, **channel ile ilerleme bildirimi**, capability), Svelte 5 + SVG ile semantik zoom. |

Öncelik yine **öğreti** tarafında. Her faz easy-git ve DockerCity'deki gibi "ne yapacağız → neden böyle → kod → ne öğrendik" akışında ayrı bir doküman olarak üretilecek.

**Kapsam kararları:**

- Uygulama **salt okunur**. Projeye hiçbir dosya yazmaz, build çalıştırmaz.
- Analiz **kaynak kod üzerinden** yapılır: JDK, Maven ya da Gradle kurulu olması gerekmez.
- Dış kütüphanelerde **sadece doğrudan** bağımlılıklar gösterilir (build dosyasında yazanlar). Transitive çözümleme Faz 11'e (opsiyonel) bırakıldı.

---

## 3. Ürün Konsepti: Ülke Haritası Metaforu

DockerCity'de şehir, easy-git'te metro vardı; java-atlas'ta bir **ülke atlası** var. Atlasın güzelliği ölçeklerdir: uzaktan bakınca bölgeleri, yaklaştıkça şehirleri, en yakında sokakları görürsün. Java projesinin hiyerarşisi de tam böyle iç içe.

| Java kavramı | Atlas karşılığı | Görsel ifade |
|---|---|---|
| Proje (kök) | **Ülke** | Tuvalin ortasındaki kara parçası |
| Build modülü (Maven module / Gradle subproject) | **Bölge (il)** | Kara içinde renkli bölge, alanı kod hacmine göre |
| JPMS modülü (`module-info.java`) | **Gümrüklü sınır** | Bölgenin kenarı kalın, çift çizgi; `exports` edilen paketler sınırda **kapı** simgesi |
| Paket | **Şehir** | Bölge içinde alt alan; adı paketin kısa adı |
| Tip (class, interface, enum, record, annotation) | **Bina** | Şehir içinde küçük blok; şekli türüne göre, boyutu satır sayısına göre |
| Paketler arası bağımlılık | **Karayolu** | Kalınlığı import sayısına göre |
| Modüller arası bağımlılık | **Otoyol** | Bölge sınırları arasında kalın, kenarlı yol |
| JPMS `requires` | **Sınır kapısı geçişi** | Otoyolun sınırı kestiği yerde kapı simgesi |
| Dış kütüphane (Maven artifact) | **Denizaşırı ülke (ada)** | Kara parçasının sağındaki denizde ada; aynı `groupId` aynı takımada |
| Dış bağımlılık | **Feribot hattı** | Kıyıdaki limandan adaya kesikli deniz yolu |
| JDK paketleri (`java.*`, `javax.*`, `jdk.*`) | **Anakara** | Varsayılan gizli; açılınca haritanın solunda tek bir kıyı şeridi |
| Döngüsel bağımlılık | **Trafik kilidi** | Döngüdeki yollar kırmızı, şehirlerde uyarı rozeti |
| JPMS ihlali (export edilmemiş pakete erişim) | **Kaçak geçiş** | Sınırı kapı olmadan kesen kırmızı kesikli yol |
| Test kodu (`src/test/java`) | **Şantiye** | Varsayılan gizli; açılınca taralı binalar |

### 3.1 Semantik zoom — üç ölçek

Haritada aynı anda her şey çizilmez. Yakınlaştırma seviyesi neyin görüneceğini belirler:

| Ölçek | Görünenler | Yollar | Cevapladığı soru |
|---|---|---|---|
| **Ülke** (uzak) | Bölgeler (build modülleri), adalar (dış kütüphaneler) | Otoyollar, feribot hatları | "Modüller birbirine ve dış dünyaya nasıl bağlı?" |
| **Bölge** (orta) | Seçili bölgenin şehirleri (paketleri), diğer bölgeler soluk | Karayolları; bölge dışına çıkanlar sınırda toplanır | "Bu modülün içi nasıl, hangi paketi dışarı bağımlı?" |
| **Şehir** (yakın) | Seçili paketin binaları (tipleri) | Tip → tip sokakları | "Bu paketteki hangi sınıf neye dokunuyor?" |

Fare tekerleği sürekli zoom yapar; belirli eşiklerde ölçek değişir. Bir bölgeye çift tıklamak o bölgeye "uçar".

### 3.2 Java'nın kafa karıştıran gerçekleri (planın kenar durumları)

easy-git'teki "git'in kafa karıştıran gerçekleri" listesinin karşılığı:

1. **Aynı paketteki sınıflar import edilmez.** `Account` sınıfı aynı paketteki `Money`'yi import satırı olmadan kullanır. Sadece import'lara bakan bir analiz paket içi bağımlılıkları **hiç görmez**. Çözüm: tip gövdesindeki `type_identifier`'ları toplayıp aynı paketin sembol tablosuyla eşlemek (Faz 3).
2. **Joker import (`import com.x.*;`).** Hangi tipin kullanıldığı belli değil. Paket seviyesinde kesin bir kenar üretir; tip seviyesinde, gövdedeki adlar o paketin sembol tablosuyla eşlenerek çözülür.
3. **Tam nitelikli ad kullanımı.** `java.util.List<String> x = ...` import olmadan yazılabilir. tree-sitter'da `scoped_type_identifier` olarak görünür; ayrıca toplanmalı.
4. **Static import.** `import static org.junit.Assert.assertEquals;` — son parça tip değil üye adıdır. Sondan bir önceki parça tipe, ondan öncesi pakete karşılık gelir.
5. **İç içe tipler.** `import com.x.Outer.Inner;` — `Outer` bir paket değil bir tiptir. Sembol tablosu olmadan `com.x.Outer` paket sanılır. Çözüm: önce tüm tipleri topla, sonra çözümle (iki geçişli analiz).
6. **Paket adı ile klasör uyuşmazlığı.** Java bunu zorunlu tutmaz. Gerçek kaynak `package` bildirimidir, klasör yolu değil.
7. **Dış paketin hangi artifact'tan geldiği bilinmez.** `import org.slf4j.Logger` hangi jar'dan? Kaynak koddan bilinemez. Çözüm sırası: (a) yerel Maven/Gradle önbelleğinde jar varsa içindeki paket listesi okunur — kesin; (b) yoksa `groupId` ile paket önekinin eşleşmesine bakılır — tahmini; (c) eşleşme yoksa **"Bilinmeyen ada"**. Eşleşme güveni ayrıntı panelinde gösterilir.
8. **Gradle bir programlama dilidir.** `build.gradle(.kts)` Groovy/Kotlin kodu; tam anlamıyla okumak için Gradle'ı çalıştırmak gerekir. Biz **sezgisel** okuyacağız: `include(...)`, `project(":x")`, `"g:a:v"` dizgileri ve `libs.versions.toml` sürüm kataloğu. Dinamik yapılandırmalar görünmez — dokümanla açıklanan bir sınır.
9. **Maven'da sürüm ve özellik kalıtımı.** `<version>${jackson.version}</version>`, sürümü `parent` POM'un `dependencyManagement` bölümünde olan bağımlılıklar. Proje içindeki parent zinciri izlenir; proje dışı parent (ör. Spring Boot parent) çözülmez, sürüm "yönetilen" olarak gösterilir.
10. **JPMS'siz modüller.** Çoğu proje `module-info.java` kullanmaz. Bu durumda build modülü sınırı "yumuşak" çizilir ve JPMS ihlali kontrolü yapılmaz. `module-info.java` olan modüllerde `requires`, `requires transitive`, `requires static`, `exports ... to` ayrı ayrı modellenir.
11. **Üretilmiş kaynaklar ve kaynak kökleri.** `target/generated-sources`, Lombok, annotation processor çıktıları. Varsayılan kaynak kökleri `src/main/java` ve `src/test/java`; Maven `<sourceDirectory>`/`<testSourceDirectory>` okunur (Guava `src/` ve `test/` kullanıyor), Gradle `sourceSets` okunmaz; `target/`, `build/`, `.gradle/`, `node_modules/` taranmaz.
12. **Büyük projeler.** Binlerce `.java` dosyası. Ayrıştırma `rayon` ile paraleldir; ilerleme ön yüze canlı bildirilir. Şehir ölçeğinde bile görünür alan dışındaki binalar çizilmez (Faz 9).

---

## 4. Teknoloji Yığını

| Alan | Seçim | Not |
|---|---|---|
| Çekirdek dil | **Rust** stable | Edition 2024, `rust-version = 1.90` |
| Masaüstü kabuğu | **Tauri 2** | Windows'ta WebView2, Linux'ta WebKitGTK, macOS'ta WKWebView |
| Java ayrıştırma | **`tree-sitter` + `tree-sitter-java`** | Hataya dayanıklı (yarım/bozuk dosyada da ağaç verir), hızlı. C kodu derler → Windows'ta MSVC, Linux'ta gcc/clang gerekir |
| POM okuma | **`quick-xml`** | Akış tabanlı; DOM kurmadan sadece ihtiyaç duyulan elemanlar okunur |
| Sürüm kataloğu | **`toml`** | `gradle/libs.versions.toml` |
| Dosya gezme | **`ignore`** | `.gitignore`'a saygılı, hızlı dizin gezintisi |
| Paralellik | **`rayon`** | Dosya başına bağımsız ayrıştırma — "utandırıcı derecede paralel" iş |
| Jar içi paket listesi | `zip` (Faz 3) | Sadece yerel önbellekte jar varsa |
| Ön yüz | **Svelte 5 + TypeScript + Vite** | easy-git ile aynı |
| Çizim | **SVG** | Semantik zoom sayesinde aynı anda çizilen eleman sayısı sınırlı |
| Rust ↔ TS tipleri | **`ts-rs`** | DTO'lardan TypeScript tipleri |
| Ayarlar / son projeler | `tauri-plugin-store` | |
| Klasör seçici | `tauri-plugin-dialog` | |
| Hata yönetimi | `thiserror` | `anyhow` sadece testlerde |
| Test | `cargo test` + `insta` (snapshot) | Ön yüz için Vitest (Faz 5+) |

### 4.1 Faz 0'da doğrulanan detaylar

- tree-sitter'ın C derleyicisi ihtiyacı: Windows'ta *Desktop development with C++* iş yükü (easy-git'te zaten kuruldu), Ubuntu'da `build-essential`.
- `tree-sitter` 0.27 ile `tree-sitter-java` 0.23 uyumu (dil `tree_sitter_java::LANGUAGE.into()` ile bağlanır).
- Tauri 2'nin Linux bağımlılıkları (`libwebkit2gtk-4.1-dev` vb.).
- crates.io'da Tauri 3 alpha görünüyor; **Tauri 2.x'te kalıyoruz** (easy-git ile aynı, kararlı).

---

## 5. Mimari

### 5.1 Klasör yapısı

```
coding-vibes/java-atlas/
├── Cargo.toml                     # Cargo workspace kökü
├── package.json                   # Vite + Svelte + Tauri CLI
├── README.md
├── docs/
│   ├── PLAN.md                    # bu doküman
│   └── tutorial/                  # 00-…, 01-… fazlar
├── crates/
│   ├── java-atlas-core/           # Domain modeli, graf analizi, yerleşim algoritması
│   │                              # dosya sisteminden, tree-sitter'dan ve Tauri'den habersiz
│   └── java-atlas-scan/           # Diskteki proje → core'un anladığı "olgular" (facts)
│       ├── build_files/           #   pom.xml, settings.gradle(.kts), build.gradle(.kts), libs.versions.toml
│       └── java/                  #   tree-sitter ile .java ve module-info.java
├── src-tauri/                     # Tauri uygulaması: command'lar, state, plugin'ler
├── src/                           # Svelte + TypeScript ön yüz
│   └── lib/
│       ├── atlas/                 # SVG bileşenleri: Region, City, Building, Road, Island, Legend
│       ├── panels/                # Kenar çubuğu, detay paneli, tooltip
│       └── bindings/              # ts-rs'nin ürettiği tipler
└── samples/                       # Örnek Java projeleri (bkz. 5.6)
```

**Bağımlılık yönü:** `src-tauri → java-atlas-scan → java-atlas-core`. Core'un `[dependencies]` bölümü boş.

### 5.2 Neden "olgular" (facts) ara katmanı?

Tarayıcı (scan) ile analiz (core) arasında bilinçli bir sınır var. Tarayıcı **ne gördüğünü** söyler, yorum yapmaz:

```rust
// java-atlas-core::facts — tarayıcının ürettiği ham bilgi
pub struct ProjectFacts {
    pub root: String,
    pub build_system: BuildSystem,             // Maven | Gradle | Plain
    pub modules: Vec<ModuleFacts>,
}

pub struct ModuleFacts {
    pub id: String,                            // ":bank-service" ya da "com.atlasbank:bank-service"
    pub path: String,                          // köke göre göreli klasör
    pub module_deps: Vec<String>,              // diğer build modüllerinin id'leri
    pub external_deps: Vec<ExternalDep>,       // doğrudan dış bağımlılıklar
    pub jpms: Option<JpmsFacts>,               // module-info.java varsa
    pub files: Vec<SourceFileFacts>,
}

pub struct SourceFileFacts {
    pub path: String,
    pub package: String,                       // "" = default package
    pub is_test: bool,
    pub lines: u32,
    pub imports: Vec<ImportFacts>,             // ham import satırları + satır numarası
    pub types: Vec<TypeFacts>,                 // dosyada tanımlanan tipler (iç içe dahil)
    pub referenced_names: Vec<NameRef>,        // gövdede geçen tip adları (basit ve nitelikli)
}
```

Core bu olguları alıp **çözümler**: hangi import iç tipe, hangisi JDK'ya, hangisi dış kütüphaneye gidiyor. Böylece:

- Analiz testleri gerçek dosya gerektirmeden, elle kurulan küçük `ProjectFacts` nesneleriyle milisaniyelerde koşar (easy-git'teki `FakeSource` yaklaşımı).
- İleride Kotlin ya da bytecode tarayıcısı eklemek istenirse core'a dokunulmaz — yeni bir tarayıcı aynı olguları üretir.

### 5.3 Core domain modeli

```rust
pub struct NodeId(u32);                        // arena indeksi

pub enum NodeKind {
    Module { jpms: Option<JpmsInfo> },
    Package { exported: Exported },            // Yes | No | ToModules(Vec<..>) | NotApplicable
    Type { kind: TypeKind, lines: u32 },       // Class | Interface | Enum | Record | Annotation
    External { group: String, artifact: String, version: Option<String> },
    Jdk { package: String },
    Unknown { package: String },
}

pub struct Node {
    pub id: NodeId,
    pub kind: NodeKind,
    pub name: String,                          // kısa ad
    pub fqn: String,                           // tam ad
    pub parent: Option<NodeId>,                // tip → paket → modül
    pub is_test: bool,
}

pub enum EdgeKind {
    ModuleDep,                                 // build dosyasında modül → modül
    JpmsRequires { transitive: bool, is_static: bool },
    ExternalDep { scope: Scope },              // compile | runtime | test | provided
    Uses,                                      // kaynak koddan türetilen tip → tip / paket → paket
}

pub struct Edge {
    pub from: NodeId,
    pub to: NodeId,
    pub kind: EdgeKind,
    pub weight: u32,                           // kaç import/kullanım bu kenarı oluşturdu
    pub evidence: Vec<Evidence>,               // dosya + satır (ilk N tanesi)
}

pub struct Atlas {
    pub nodes: Vec<Node>,
    pub edges: Vec<Edge>,
    pub cycles: Vec<Cycle>,                    // paket seviyesindeki SCC'ler
    pub violations: Vec<Violation>,            // JPMS ihlalleri, (Faz 10) mimari kural ihlalleri
    pub metrics: HashMap<NodeId, Metrics>,     // Ca, Ce, I
}
```

**Toplama (aggregation):** Kaynak koddan sadece tip → tip `Uses` kenarları çıkar. Paket → paket ve modül → modül kenarları bunlardan **yukarı doğru toplanarak** türetilir (`weight` toplanır). Ülke ölçeğindeki otoyol kalınlığı bu yüzden "bu iki modül arasında kaç gerçek kullanım var" demektir — build dosyasında yazan bağımlılıkla karşılaştırılabilir:

- Build dosyasında var, kodda kullanım yok → **"Kullanılmayan bağımlılık"** (soluk yol, uyarı)
- Kodda kullanım var, build dosyasında yok → derlenmez ama transitive yoldan geliyor olabilir → **"Beyan edilmemiş bağımlılık"** (sarı uyarı)

Bu karşılaştırma ürünün en bilgilendirici çıktılarından biri.

### 5.4 Analiz adımları

1. **Sembol tablosu.** Tüm dosyalardaki tipler `fqn → NodeId` haritasına yazılır (iç içe tipler `Outer.Inner` ve `Outer$Inner` olarak).
2. **Import çözümleme.** Her import için en uzun eşleşen önek aranır: tip mi (iç içe tip dahil), paket mi (joker import), JDK mı, dış mı. Static import'larda son parça atılır.
3. **Ad çözümleme.** Gövdedeki basit adlar şu sırayla eşlenir: tek tip import'ları → aynı paket → joker import'lar → `java.lang`. Nitelikli adlar doğrudan sembol tablosunda aranır.
4. **Dış artifact eşleme.** Bkz. 3.2/7.
5. **Toplama.** Tip → paket → modül.
6. **Döngüler.** Paket grafında **Tarjan'ın güçlü bağlı bileşenler** algoritması; boyutu 1'den büyük her bileşen bir döngüdür. Döngüyü "kıracak" en hafif kenar önerilir (ağırlığı en düşük kenar).
7. **JPMS kontrolleri.** A modülündeki bir tip B modülündeki bir pakete erişiyorsa: B o paketi export ediyor mu (ya da `exports ... to A`)? A, B'yi `requires` ediyor mu (doğrudan ya da `requires transitive` zinciriyle)? Değilse **kaçak geçiş**.
8. **Metrikler.** Robert C. Martin'in paket metrikleri — JDepend'in de hesapladıkları:
   - **Ca** (afferent coupling): bu pakete bağımlı dış paket sayısı
   - **Ce** (efferent coupling): bu paketin bağımlı olduğu dış paket sayısı
   - **I = Ce / (Ca + Ce)** — kararsızlık; 0 = çok kullanılan, sağlam; 1 = kimse kullanmıyor, kolay değişir
   - (Faz 10) **A** soyutluk ve **D** ana diziden uzaklık

**Rust öğretisi açısından kritik karar:** easy-git'teki gibi graf **indeks tabanlı (arena)**. Burada ek olarak **string interning** de anlatılacak: binlerce dosyada tekrar eden paket adları için her seferinde `String` ayırmak yerine bir kez saklayıp `u32` ile anmak.

### 5.5 Yerleşim (layout)

Yerleşim **Rust tarafında, core içinde** hesaplanır; ön yüz sadece çizer. Böylece algoritma test edilebilir ve aynı proje her açılışta aynı haritayı verir (deterministik).

- **Kara parçası ve bölgeler:** *Squarified treemap* (Bruls, Huizing, van Wijk). Alan = modüldeki satır sayısı (en az bir taban alan, boş modüller kaybolmasın). Treemap kareye yakın dikdörtgenler üretir → okunur bölgeler.
- **Şehirler:** Aynı algoritma bölge içinde, paketler için. Alt paketler (`com.x.service.account`) üst paketin şehri içinde mahalle olarak yuvalanır.
- **Binalar:** Şehir içinde ızgara; tür ve boyuta göre sıralı.
- **Adalar:** Kara parçasının sağındaki deniz şeridinde, `groupId`'ye göre gruplanmış takımadalar; en çok kullanılan en yukarıda.
- **Yollar:** Bölge/şehir merkezleri arası. MVP'de iki noktalı yumuşak eğri, kalınlık `log2(weight)` ile ölçekli. Faz 9'da kenar demetleme (edge bundling) değerlendirilir.
- **Kenarları "organik" göstermek** (dikdörtgen bölgelere hafif dalgalı sınır) ön yüzde salt görsel bir SVG filtresi — yerleşimi değiştirmez. Opsiyonel, Faz 9.

### 5.6 Örnek projeler (fixture)

DockerCity'deki `samples/docker-compose.yml`, easy-git'teki "Metro Hattı Hikâyesi" repo'sunun karşılığı. İki örnek, `samples/` altında gerçek dosyalar olarak (Faz 1'de yazılır):

**`samples/atlas-bank/`** — Maven, çok modüllü, kısmen JPMS

| # | İçerik | Test ettiği kenar durum |
|---|---|---|
| 1 | Kök `pom.xml` (`packaging=pom`), `<modules>` ile 5 modül, `<properties>` içinde sürümler, `dependencyManagement` | Parent kalıtımı, `${...}` çözümleme |
| 2 | `bank-common` → hiçbir iç modüle bağımlı değil | Yaprak bölge |
| 3 | `bank-domain` → `bank-common`; `module-info.java` var, `requires transitive` ile `bank-common`, `domain.model` ve `domain.rules` export ediliyor | JPMS sınırı, transitive requires |
| 4 | `bank-persistence` → `bank-domain`; `persistence.internal` paketi export edilmiyor | Gizli şehir |
| 5 | `bank-service` → `bank-domain`, `bank-persistence`; `service.account` ↔ `service.audit` döngüsü | Döngü (trafik kilidi) |
| 6 | `bank-api` → `bank-service`; bir sınıf `persistence.internal`'ı kullanıyor | JPMS kaçak geçiş + beyan edilmemiş modül bağımlılığı |
| 7 | Aynı paket içi kullanım (import'suz), joker import, static import, tam nitelikli ad, iç içe tip import'u | 3.2'deki 1–5 numaralı durumlar |
| 8 | Dış: `slf4j-api` (compile), `jackson-databind` (compile, sürüm property'den), `junit-jupiter` (test); `bank-common`'da beyan edilmiş ama kullanılmayan `commons-lang3` | Adalar, test kapsamı, kullanılmayan bağımlılık |

**`samples/atlas-shop-gradle/`** — Gradle Kotlin DSL, 3 alt proje, `libs.versions.toml`, JPMS yok. Gradle sezgisel okuyucusunun testi.

Beklenen sayılar (modül, paket, tip, kenar, döngü, ihlal) Faz 1–3 dokümanlarında örnekler yazıldıkça kesinleşir ve `insta` snapshot'larıyla sabitlenir.

### 5.7 Tauri köprüsü

```rust
#[tauri::command] async fn open_project(path: String, on_progress: Channel<ScanProgress>, state: State<'_, AppState>) -> Result<ProjectSummary, AppError>;
#[tauri::command] async fn get_atlas_view(scale: Scale, focus: Option<String>, filters: ViewFilters, state: State<'_, AppState>) -> Result<AtlasViewDto, AppError>;
#[tauri::command] async fn get_node_details(id: String, state: State<'_, AppState>) -> Result<NodeDetailsDto, AppError>;
#[tauri::command] async fn get_edge_evidence(from: String, to: String, state: State<'_, AppState>) -> Result<Vec<EvidenceDto>, AppError>;
```

- **Yeni öğreti — `tauri::ipc::Channel`:** Tarama uzun sürebilir. easy-git'te tek seferlik cevap yeterliydi; burada `Channel<ScanProgress>` ile "312/1840 dosya ayrıştırıldı" gibi akan ilerleme mesajları gönderilir.
- Ağır iş `spawn_blocking` içinde; ayrıştırma içeride `rayon` ile paralel.
- Ön yüz bütün atlası değil, **o anki ölçek ve odak için görünümü** ister (`get_atlas_view`). Büyük projede IPC yükü küçük kalır.
- Capabilities: `dialog:allow-open`, `core:default`, tema için `core:window:allow-set-theme`. Başka hiçbir şey.

---

## 6. Görsel Tasarım Kararları

| Konu | Karar |
|---|---|
| Deniz | Düz, sakin mavi-gri (koyu: `#14202B`, açık: `#E6EEF3`); çok hafif kontur çizgileri |
| Kara | Nötr toprak tonu; bölgeler kendi renginin düşük doygunluklu tonunda dolgu, ince beyaz/koyu sınır |
| JPMS sınırı | 3px çift çizgi; export edilen paketin şehri sınıra bakan kenarında küçük kapı simgesi |
| JPMS'siz bölge | 1px kesikli sınır ("yumuşak sınır") |
| Şehir | Bölge içinde daha açık dolgu, sol üstte ad; alt paketler mahalle olarak iç içe |
| Bina | Class: kare, Interface: içi boş kare, Enum: altıgen, Record: yuvarlatılmış kare, Annotation: üçgen. Boyut `sqrt(lines)` |
| Otoyol / karayolu | Kalınlık `1 + log2(weight)` px, en fazla 10px; yön için yolun ortasında küçük ok |
| Feribot hattı | Kesikli, deniz renginin koyu tonu; test kapsamı daha soluk |
| Ada | Yuvarlak hatlı kara parçası, üstünde `artifactId`, altında sürüm; eşleşme güveni düşükse kenar kesikli |
| Döngü | Yol ve şehir rozeti kırmızı (`#D64545` / koyu temada `#FF6B6B`) |
| Kaçak geçiş | Kırmızı kesikli yol + sınırda ⚠ işareti |
| Kullanılmayan bağımlılık | %35 opaklık, gri |
| Bölge paleti | 8 renk, renk körlüğü doğrulamalı (easy-git paleti yeniden kullanılabilir) |

### 6.1 Ekran düzeni

```
┌────────────────────────────────────────────────────────────────────────────┐
│ [📂 Proje seç ▾]  atlas-bank · Maven · 5 modül · 23 paket   Ülke ▸ bank-service   │
│                                    [☐ Test] [☐ JDK] [☑ Dış] [Ara…]   ⟳     │
├──────────────────┬─────────────────────────────────────────────────────────┤
│ ATLAS            │ ┌──────────────┬─────────────┐            ~~~~~~~~~~~  │
│ ▾ bank-api       │ │  bank-api    │ bank-service│══════╗     ( slf4j )    │
│ ▾ bank-service ⚠ │ │   ▢ ▢        │  ▢ ⟲ ▢      │      ║    ~~~~~~~~~~~  │
│   ◉ account  ⟲   │ ├──────────────┼─────────────┤      ╚═ ─ ─( jackson ) │
│   ◉ audit    ⟲   │ │ bank-persist.│ bank-domain ▣│           ~~~~~~~~~~~  │
│ ▸ bank-domain ▣  │ │   ▢ 🔒        │  ▢ ▢ ▢      │           ( junit )·· │
│ ▸ bank-common    │ └──────────────┴─────────────┘                         │
│ ADALAR           │                                         [lejant]       │
│ ◌ commons-lang3 ✕│                                                         │
├──────────────────┴─────────────────────────────────────────────────────────┤
│ com.atlasbank.service.account · bank-service · 6 tip · Ca 2 · Ce 4 · I 0.67     │
│ ⟲ audit ile döngüde — kırılması önerilen kenar: account → audit (2 kullanım)  │
└────────────────────────────────────────────────────────────────────────────┘
```

Üstteki **ekmek kırıntısı** (`Ülke ▸ bank-service ▸ account`) hangi ölçekte olunduğunu gösterir ve bir üst ölçeğe tek tıkla dönülür.

---

## 7. Bilgilendirici Detaylar

**Katman 1 — Her zaman görünür (kenar çubuğu + üst çubuk):**
- Modül/paket ağacı, döngü (⟲), JPMS (▣), gizli paket (🔒) rozetleri
- Adalar listesi; kullanılmayan bağımlılık (✕), beyan edilmemiş bağımlılık (!)
- Proje özeti: build sistemi, modül, paket, tip, dosya sayısı, tarama süresi

**Katman 2 — Hover (tooltip):**
- Bölge/şehir/bina: ad, tip sayısı, satır, Ca/Ce
- Yol: kaynak → hedef, kullanım sayısı
- Hover'da o düğümün **giden yolları mavi, gelen yolları turuncu** vurgulanır, gerisi soluklaşır

**Katman 3 — Tıklama (alt detay paneli):**
- Düğüm: tam ad, metrikler (Ca, Ce, I), içerdiği tipler, bağımlı olduğu ve ona bağımlı olan düğümler (tıklanabilir)
- Yol: **kanıt listesi** — bu bağımlılığı oluşturan her import/kullanım, `dosya:satır` olarak; kopyalanabilir yol
- Ada: koordinatlar (`g:a:v`), kapsam, eşleşme yöntemi (jar / önek / bilinmiyor), hangi modüllerin beyan ettiği, kodda kaç yerde kullanıldığı

---

## 8. Yol Haritası

MVP = **Faz 0 → Faz 7**. Sonrası cila ve genişleme.

### Faz 0 — Atölye kurulumu ✅
`docs/tutorial/00-kurulum-ve-iskelet.md`

- Araç zinciri (Rust, C/C++ derleyici, Node.js, Linux'ta WebKitGTK)
- Kökte Cargo workspace: `crates/java-atlas-core`, `crates/java-atlas-scan`, `src-tauri`
- Svelte 5 + TypeScript + Vite ön yüz, Tauri 2 kabuğu, dialog eklentisi ile klasör seçimi
- tree-sitter-java duman testi: "C derleyicisi çalışıyor, gramer bağlandı"

**Kazanım:** Workspace, Tauri'nin iki süreçli yapısı, `build.rs` ile C kodu derleyen crate'ler, capabilities.
**Çıktı:** Pencere açılıyor, "Proje seç" bir klasör seçtiriyor, `cargo test --workspace` yeşil.

---

### Faz 1 — Build dosyalarını okumak ✅
`docs/tutorial/01-build-dosyalari.md`

- `samples/atlas-bank` ve `samples/atlas-shop-gradle` örnek projelerinin yazılması
- Proje keşfi: kökte `pom.xml` mi, `settings.gradle(.kts)` mi, hiçbiri mi (Plain)
- `quick-xml` ile POM: `modules`, `parent`, `properties`, `dependencies`, `dependencyManagement`, `${...}` çözümleme
- Gradle sezgisel okuyucu: `include(...)`, `project(":x")`, koordinat dizgileri, `libs.versions.toml`
- `ModuleFacts` (kaynak dosyalar hariç) üretimi, `thiserror` ile hata tipleri

**Kazanım:** Akış tabanlı XML ayrıştırma, olay döngüsü ile durum makinesi, `toml` + `serde`, el yazımı küçük tokenizer.
**Çıktı:** İki örnek projenin modül grafı ve doğrudan dış bağımlılıkları testlerle doğru okunuyor.

---

### Faz 2 — Java kaynaklarını okumak ✅
`docs/tutorial/02-tree-sitter-ile-java.md`

- tree-sitter temelleri: `Parser`, `Tree`, `Node`, `TreeCursor`, düğüm türleri
- `package`, `import` (tekli, joker, static), tip bildirimleri (iç içe dahil), satır sayısı
- Gövdedeki tip adlarının toplanması (`type_identifier`, `scoped_type_identifier`)
- `module-info.java`: `requires` (transitive/static), `exports` (… to), `opens`, `uses`, `provides`
- `ignore` ile dizin gezme, `rayon` ile paralel ayrıştırma, bozuk dosyada hataya dayanıklılık
- Sorgu (query) dili ile alternatif çözüm — kıyas

**Kazanım:** Somut sözdizimi ağacı, FFI arkasındaki C kütüphanesi, yaşam süreleri (`Node<'tree>`), paralel iteratörler, `Send`/`Sync`.
**Çıktı:** Örnek projelerin tüm `SourceFileFacts`'ları üretiliyor; snapshot testi.

---

### Faz 3 — Bağımlılık grafı ve analiz ✅
`docs/tutorial/03-graf-ve-analiz.md`

- Sembol tablosu, string interning, arena graf
- 5.4'teki çözümleme adımları; dış artifact eşleme (jar → önek → bilinmiyor)
- Toplama: tip → paket → modül
- Tarjan SCC ile döngüler, kırılma önerisi
- JPMS kontrolleri, kullanılmayan/beyan edilmemiş bağımlılıklar
- Ca / Ce / I metrikleri

**Kazanım:** Graf algoritmaları, iki geçişli çözümleme, `HashMap` entry API, iterator zincirleri, snapshot testing.
**Çıktı:** `atlas-bank`'ta beklenen döngü, kaçak geçiş, kullanılmayan bağımlılık ve metrikler `insta` ile doğrulanıyor.

---

### Faz 4 — Tauri köprüsü ✅
`docs/tutorial/04-tauri-kopru.md`

- `AppState`, command'lar, `spawn_blocking`
- `Channel<ScanProgress>` ile canlı ilerleme
- DTO'lar, `ts-rs` ile TypeScript tipleri, ölçek/odak bazlı görünüm isteği
- Son açılan projeler (`tauri-plugin-store`), capabilities

**Kazanım:** Tauri IPC'nin akış (stream) tarafı, async/blocking ayrımı, DTO tasarımı.
**Çıktı:** Proje seçilince ilerleme çubuğu akıyor, konsolda atlas özeti görünüyor.

---

### Faz 5 — Harita yerleşimi ve ilk çizim ✅
`docs/tutorial/05-treemap-ve-cizim.md`

- Squarified treemap (Rust, core içinde), test edilebilir geometri
- Ada yerleşimi
- `AtlasView.svelte`: SVG tuvali, `viewBox` ile pan/zoom
- Ülke ölçeğinde bölgeler ve adalar

**Kazanım:** Yerleşim algoritmaları, en-boy oranı optimizasyonu, SVG koordinat sistemi, Svelte 5 rune'ları.
**Çıktı:** `atlas-bank` açılınca 5 bölge ve 4 ada görünüyor.

---

### Faz 6 — Yollar ve semantik zoom ✅
`docs/tutorial/06-yollar-ve-zoom.md`

- Otoyol, karayolu, feribot hattı çizimi; kalınlık ölçeği; yön okları
- Üç ölçek arasında geçiş, "uçarak" yaklaşma animasyonu, ekmek kırıntısı
- Döngü ve kaçak geçiş görselleri, lejant
- Koyu/açık tema

**Kazanım:** Seviye-ayrıntı (LOD) mantığı, animasyonlu `viewBox` geçişi, türetilmiş görsel durum.
**Çıktı:** Üç ölçek çalışıyor; döngü ve ihlal haritada görünüyor.

---

### Faz 7 — Etkileşim ve bilgi panelleri *(MVP tamamlanır)*
`docs/tutorial/07-etkilesim-ve-paneller.md`

- Hover vurgulama (gelen/giden), tooltip
- Detay paneli: metrikler, kanıt listesi (`dosya:satır`), tıklanabilir komşular
- Kenar çubuğu: ağaç, rozetler, adalar; filtreler (test, JDK, dış)
- Arama (tip/paket adı) → haritada o noktaya uçma

**Kazanım:** Bileşenler arası state, olay yönetimi, klavye erişilebilirliği.
**Çıktı:** **Çalışan MVP.** Proje seç, atlası gez, döngüleri ve ihlalleri kanıtıyla incele.

---

### Faz 8 — Canlı izleme
`notify` ile `.java` ve build dosyası değişikliklerini izleme, debounce, **sadece değişen dosyanın** yeniden ayrıştırılması (olgular dosya bazlı olduğu için mümkün), haritanın kendiliğinden güncellenmesi.

### Faz 9 — Büyük projeler
Tarama önbelleği (dosya yolu + mtime + boyut → olgular, diskte), görünür alan sanallaştırması, kenar demetleme, performans ölçümü (`cargo flamegraph`, tarayıcı performans sekmesi). Gerçek bir açık kaynak projeyle (ör. birkaç bin dosyalık bir Apache projesi) deneme.

### Faz 10 — Mimari kurallar ve dışa aktarma
Proje köküne konan `atlas-rules.toml` ile katman kuralları (`"..domain.." bağımlı olamaz "..api.."`), ihlallerin haritada gösterimi; soyutluk (A) ve ana diziden uzaklık (D) metrikleri ile **A–I grafiği**; SVG/PNG dışa aktarma; Tauri bundler ile Windows (MSI/NSIS), Linux (AppImage/deb) ve macOS (dmg) paketleri.

### Faz 11 — İleri seviye *(opsiyonel)*
Yerel `~/.m2` önbelleğinden transitive bağımlılık çözümleme, Kotlin kaynak desteği (`tree-sitter-kotlin`), iki commit arasındaki atlas farkı ("bu PR hangi yolları ekledi?").

---

## 9. Doküman Düzeni

easy-git ve DockerCity ile aynı kalıp:

1. **Bu bölümde ne yapacağız**
2. **Neden böyle** — alternatifler ve tercih gerekçesi
3. **Adım adım uygulama** — tam kod blokları, dosya yollarıyla
4. **Takıldığın yerler** — bilinen tuzaklar ve çözümleri
5. **Ne öğrendik**
6. **Kendin dene** — 2-3 küçük görev

Dosya adlandırma: `NN-konu-adi.md`, `docs/tutorial/` altında. Dokümanlar Türkçe, kod içi metinler ve yorumlar İngilizce.

---

## 10. MVP Kabul Kriterleri

- [ ] Klasör seçici ile yerel bir Java projesi açılabiliyor; Java projesi olmayan klasörde anlaşılır mesaj veriliyor
- [ ] Maven (`atlas-bank`) ve Gradle (`atlas-shop-gradle`) örnekleri doğru modül grafıyla okunuyor
- [ ] Tarama sırasında ilerleme gösteriliyor
- [ ] Ülke ölçeğinde bölgeler, otoyollar, adalar ve feribot hatları görünüyor
- [ ] Bölge ve şehir ölçeklerine yakınlaşılabiliyor; ekmek kırıntısıyla geri dönülebiliyor
- [ ] `service.account ↔ service.audit` ve `persistence ↔ persistence.internal` döngüleri kırmızı; `bank-api → persistence.internal` kaçak geçiş olarak işaretli
- [ ] `commons-lang3` kullanılmayan bağımlılık olarak soluk
- [ ] Bir yola tıklayınca kanıt listesi (`dosya:satır`) görünüyor
- [ ] Ca / Ce / I metrikleri detay panelinde
- [ ] `cargo test --workspace` yeşil (core birim + örnek proje snapshot testleri)
- [ ] Uygulama Windows 11 ve Ubuntu'da çalışıyor
- [ ] Faz 0–7 dokümanları yazılmış

---

## 11. Riskler ve Açık Konular

| Konu | Not / önerilen yaklaşım |
|---|---|
| Gradle'ın tam okunamaması | Sezgisel okuma; okunamayan kısımlar için proje özetinde "Gradle yapılandırması kısmen okundu" uyarısı. İleride opsiyonel: kurulu Gradle varsa `gradle -q projects` çıktısından modül listesi. |
| Ad çözümlemenin %100 olmaması | Derleyici değiliz: yerel değişken adı ile tip adı çakışması, generic tip parametreleri (`T`), `var` gibi durumlar yanlış pozitif/negatif üretebilir. Generic parametreler bildirildikleri kapsamda filtrelenir; geri kalanı dokümanda "bilinen sınırlar". Paket seviyesindeki sonuçlar tip seviyesine göre çok daha güvenilir. |
| tree-sitter-java sürüm uyumu | Gramer crate'i ile çekirdek crate'in uyumlu sürümleri Faz 0'da sabitlendi; tree-sitter kullanımı tamamen `java-atlas-scan` içinde — değişiklik etkisi tek crate'le sınırlı. |
| Treemap'in "harita gibi" görünmemesi | Dikdörtgen bölgeler atlas hissini zayıflatabilir. MVP'de net okunurluk öncelikli; organik sınırlar (Voronoi treemap ya da SVG filtresi) Faz 9'da değerlendirilir. |
| Linux/WebKitGTK farkları | easy-git'te `context-stroke` desteklenmiyordu, ok başları ayrı `path` ile çizildi. Aynı kural baştan uygulanacak; SVG filtreleri her iki platformda denenecek. |
| Windows'ta derleme ortamı | easy-git'te Build Tools eksikliği "Developer Command Prompt" ile çözülmüştü. tree-sitter de C derleyicisi istediği için Faz 0 dokümanında aynı not yer alıyor. |
| Kapsam kayması | Ada animasyonları, organik kıyılar, 3B binalar cazip. MVP'den önce Faz 8+'a atlanmaması öneriliyor. |

---

## 12. Uygulama Notları

*(Fazlar ilerledikçe plandan ayrılan ya da netleşen noktalar buraya eklenecek — easy-git'teki tablo gibi.)*

| Konu | Plan | Uygulama | Neden |
|---|---|---|---|
| Tauri sürümü | 2.x | 2.x (crates.io'da 3.0 alpha var) | Kararlı sürüm; easy-git ile aynı kalıplar |
| Örnek projeler | Faz 1'de build dosyaları | Build dosyaları **ve Java kaynakları** Faz 1'de yazıldı | Faz 2–3 aynı örneklerle çalışacak; Maven örneği `javac` ile (JPMS dahil), Gradle örneği `gradle dependencies` ile doğrulandı |
| `bank-domain` JPMS | Sadece `domain.model` export | `model` + `rules` export | `bank-service` `domain.rules`'u kullanıyor; tek ve net bir kaçak geçiş örneği (`bank-api → persistence.internal`) kalsın diye |
| `atlas-shop-gradle` | `implementation(project(":shop-core"))` | `api(project(":shop-core"))` | `Product` public API'sinde `Sku` var; `implementation` ile `shop-web` derlenmezdi. `ModuleDep.exposed` alanı bu yüzden eklendi |
| Modül bağımlılığı olgusu | `module_deps: Vec<String>` | `Vec<ModuleDep { target, scope, exposed }>` | Test kapsamlı modül bağımlılıkları ve Gradle `api` bilgisi kaybolmasın |
| Gradle kök projesi | — | Alt projesi olan ve `src/` klasörü olmayan kök **toplayıcı** sayılır | Maven'daki `packaging=pom` karşılığı |
| Kaynak klasörleri | Sadece `src/main/java`, `src/test/java` | Maven `<sourceDirectory>`/`<testSourceDirectory>` da okunuyor (kalıtımlı); `ModuleFacts.source_roots` | Guava ile denemede 0 dosya okundu; yaygın bir düzen |
| Tip adı adayları | `type_identifier` + `scoped_type_identifier` | Ek olarak büyük harfli `method_invocation`/`field_access` nesneleri, method reference'lar, annotation adları | `Money.zero()`, `LoggerFactory.getLogger` gibi statik kullanımlar ağaçta `identifier`; `LOG` gibi yanlış adaylar Faz 3'te eşleşmeyip düşecek |
| `module-info.java` | Sıradan dosya | Sadece `ModuleFacts.jpms`; `files` listesine girmez | Tip içermez; test kaynağındaki module-info ana olanı ezmez |
| Ayrıştırılamayan dosya | `ScanError::NoTree` | Uyarı | Tek dosya tüm taramayı durdurmamalı |
| Gramer sınırı | — | `Object @Nullable ... args` (varargs tip annotation'ı) sözdizimi hatası veriyor | tree-sitter-java 0.23; dosyanın geri kalanı okunuyor, uyarı üretiliyor |
| Ölçüm | — | Release, 2 çekirdek: Commons Lang 628 dosya ~0,45 sn; Guava 1.556 dosya ~0,85 sn (tek iş parçacığıyla ~2 sn) | Faz 9'daki önbellek ihtiyacı için referans |
| Dış artifact eşleme | jar → groupId öneki → bilinmiyor | jar → **ad puanlaması** → bilinmiyor (`MatchMethod::Jar` / `Name`) | Sadece önek, Guava'da Truth, JUnit 4, Jimfs gibi kütüphaneleri kaçırdı; puanlama artifactId kelimelerini de kullanıyor, farklı groupId'ler arasında eşitlikte tahmin yapmıyor |
| Jar indeksi | `zip` (Faz 3) | `ScanOptions { library_caches }`; `~/.m2` ve Gradle `files-2.1` düzenleri; sadece central directory okunuyor | Testler önbelleksiz (`ScanOptions::default()`), aksi hâlde snapshot'lar makineye bağlı olurdu |
| Kenar kaynağı | Tip → tip | Dosyanın **ilk üst seviye tipi** → hedef | Faz 2 referansları dosya başına topluyor; iç tipler dış tipin dosyasında |
| Ağırlık | `weight: u32` | `Weight { main, test }` | Döngüler ve metrikler sadece ana kodla; test filtresi aynı ayrımı kullanacak |
| Kullanılmayan bağımlılık | Tüm beyanlar | Kalıtılmış ve `runtime` kapsamlı beyanlar hariç | Kök POM'daki JUnit beş modülde gürültü yapıyordu; `runtime` tanımı gereği kodda görünmez |
| `atlas-bank` beklentisi | 1 döngü | **2 döngü**: planlanan `service.account ⇄ service.audit` + `persistence ⇄ persistence.internal` (fabrika/arayüz kalıbı) | Analiz haklı; gerçek projelerdeki en tipik döngü olduğu için örnekte bırakıldı. Ayrıca `bank-service → bank-common` beyan edilmemiş (dolaylı) bulundu |
| Ölçüm (analiz) | — | Guava 1.556 dosya ~50 ms, Commons Lang ~12 ms | Tarama süresinin yanında ihmal edilebilir |
| Görünüm mantığı | Tauri tarafında | Core'da `Atlas::view(scale, focus, filters)`; Tauri sadece DTO'ya çeviriyor | İş kuralı (katlama, filtreler) Tauri'siz test edilebilsin |
| Komutlar | `open_project`, `get_atlas_view`, `get_node_details`, `get_edge_evidence` | Aynı + `recent_projects`, `forget_recent_project`; ilerleme `Channel<ScanEvent>` (etiketli enum) | Kanal mesajları 40 ms'de bir kısılıyor, son dosya her zaman gönderiliyor |
| Düğüm kimliği | `NodeId` | Ön yüze **kararlı anahtar** (`m:`, `p:<modül>\|<paket>`, `t:`, `l:`, `j:`, `u:`) | İndeksler taramadan taramaya değişir; Faz 8'de seçim korunacak |
| Kanıt sorgusu | Kenar başına | `evidence_between(from, to)`: her seviye ve karışık seviyeler (paket → modül) için tip kenarlarından | Bölge görünümündeki katlanmış yolların da kanıtı olmalı |
| Ön yüz (Faz 4) | Konsolda özet | Rapor paneli + ülke verisinin liste önizlemesi; `VITE_AUTO_OPEN` ile açılışta proje | Faz 5'e kadar sonuçları görünür kılmak; ekran görüntüleri klasör seçicisiz alınabilsin |
| Türkçe arayüz | — | `text-transform: uppercase` + `lang="tr"` → "BUİLD"; İngilizce kelimeler `lang="en"` | Yerelleştirme tuzağı |
| Yerleşim kapsamı | Ölçek başına yerleşim | **Tek dünya**: kara → bölge → şehir → bina, hepsi iç içe treemap, tek koordinat sisteminde | Yakınlaşma sadece kamera hareketi; uzaktan görülen şehir yaklaşınca aynı yerde |
| Küçük parçalar | Alan tamamen orantılı | Her çocuk ebeveyninin en az %3'ü (`MIN_SHARE`) | Küçük modüller/paketler görünür ve tıklanabilir kalsın |
| Ada yerleşimi | Denizde sütun | Takımadalar **raf yerleşimiyle** (640 birim genişlik) | Tek sütun Guava'da dört sütuna taştı, kara ekranda küçücük kaldı |
| Alt paketler | Mahalle olarak iç içe | MVP'de düz (her paket bir şehir) | Faz 5 "Kendin dene" görevi |
| Çift tıklama | `dblclick` | Kendi algılama (400 ms) + sürükleme eşiği (4 px) ile pointer capture | WebKitGTK'da `dblclick` SVG grubuna gelmedi; erken capture tıklamaları `<svg>`'ye yönlendiriyordu |
| Rapor paneli | Sabit | Araç çubuğundaki **Rapor** düğmesiyle gizlenebiliyor | Harita tüm pencereyi kullanabilsin |
| Yol çizimi | Merkezden merkeze | Sınırdan sınıra, gidiş yönünün **sağına** bükülen ikinci derece Bézier; ok başı ayrı `path`, boyutu ekran pikselinde sabit | A → B ile B → A ayrı şeritler; döngüler görünür. `context-stroke` WebKitGTK'da yok |
| Ölçek geçişi | Eşikte ölçek değişir | İki eşik (içeri %55, dışarı 2,6×) + tekerlek durduktan 220 ms sonra karar | Titremeyi önleyen histerezis |
| Ölçekler arası çizim | Ölçek başına sahne | **Taban harita** (ülke) hep çizili; odak katmanı üstte; odak dışı bölgeler ve bağlantısız adalar soluk | Ölçek değişince hiçbir şey yerinden oynamıyor |
| Döngü işareti | Paket kenarlarında | Tip ve karışık seviyeli kenarlarda da (paket atasından) | Şehir ölçeğinde döngü yolları da kırmızı |
| Tema | Sistem | Sistem / Açık / Koyu (`data-theme`), `localStorage` | Kullanıcı seçimi |
| UI denemesi | Masaüstü uygulamasıyla | `scripts/ui-check.mjs`: Playwright + sahte `__TAURI_INTERNALS__` + Rust'tan üretilen fikstürler | Hızlı döngü; konsol hataları görünür |
| Kalabalık bölgeler | — | 60'tan fazla yolda `.dense`: yollar inceltilip soluklaştırılıyor | Guava'nın `guava` bölgesi okunmaz hâldeydi; gerçek çözüm Faz 9'da kenar demetleme |
| `quick-xml` | 0.3x API | 0.42: `xml10_content()`, varlıklar `Event::GeneralRef` | İnternetteki örneklerin çoğu eski API'yi kullanıyor |

---

## 13. Sonraki Adım

**Faz 7 — Etkileşim ve bilgi panelleri** (`docs/tutorial/07-etkilesim-ve-paneller.md`): ayrıntı paneli (metrikler, kanıt listesi `dosya:satır`, tıklanabilir komşular), yola tıklayınca kanıt, kenar çubuğunda modül/paket ağacı ve rozetler, arama ile bir tipe/pakete uçma; rapordaki öğelerden haritaya gitme. MVP tamamlanır.
