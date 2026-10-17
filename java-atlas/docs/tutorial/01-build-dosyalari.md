# Faz 1 — Build Dosyalarını Okumak

> **Önceki:** [00 — Kurulum ve iskelet](00-kurulum-ve-iskelet.md) · **Sonraki:** 02 — tree-sitter ile Java *(yazılacak)*

## 1. Bu bölümde ne yapacağız

Atlasın en büyük ölçeği olan **ülke haritasının** ham verisini çıkaracağız: projede hangi build modülleri (bölgeler) var, birbirlerine nasıl bağlılar ve dışarıdan hangi kütüphaneleri (adaları) istiyorlar.

Bölümün sonunda:

- `samples/` altında kenar durumlarla dolu iki örnek proje var: **`atlas-bank`** (Maven, 5 modül, kısmen JPMS) ve **`atlas-shop-gradle`** (Gradle, Kotlin + Groovy DSL, sürüm kataloğu).
- `java-atlas-core`'da tarayıcının konuştuğu dil olan **olgular** (`facts`) modülü var.
- `java-atlas-scan` bir klasöre bakıp build sistemini tanıyor ve `ProjectFacts` üretiyor.
- `cargo run -p java-atlas-scan --example inspect -- samples/atlas-bank` modül ağacını yazdırıyor.
- `cargo test --workspace` 41 testle yeşil; iki örneğin çıktısı `insta` snapshot'larıyla sabit.

```
atlas-bank · Maven · 5 module(s)

com.atlasbank:atlas-bank (aggregator)  [.]  <pom.xml>
  ⇢ org.junit.jupiter:junit-jupiter:5.11.4  test (managed)

com.atlasbank:bank-common  [bank-common]  <bank-common/pom.xml>
  ⇢ org.junit.jupiter:junit-jupiter:5.11.4  test (managed) inherited
  ⇢ org.apache.commons:commons-lang3:3.17.0  compile (managed)
...
com.atlasbank:bank-api  [bank-api]  <bank-api/pom.xml>
  → com.atlasbank:bank-service  compile
  ⇢ org.junit.jupiter:junit-jupiter:5.11.4  test (managed) inherited
  ⇢ com.fasterxml.jackson.core:jackson-databind:2.18.2  compile (${jackson.version})
  ⇢ org.slf4j:slf4j-api:2.0.16  compile (managed)
```

`→` modül bağımlılığı (otoyol), `⇢` dış bağımlılık (feribot hattı). Parantez içindeki bilgi sürümün **nereden geldiği**.

## 2. Neden böyle

### Önce örnek projeler

DockerCity'de `samples/docker-compose.yml`, easy-git'te "Metro Hattı Hikâyesi" repo'su vardı. Burada iki gerçek Java projesi yazıyoruz — ve Java kaynaklarını da şimdiden içlerine koyuyoruz, çünkü Faz 2 ve 3 aynı örneklerle çalışacak.

Örnekler süs değil, **test senaryosu**. Her tuhaflık bilerek konuldu:

**`atlas-bank` (Maven)**

| Dosya / yer | Kenar durum | Hangi fazda işe yarar |
|---|---|---|
| Kök `pom.xml`, `packaging=pom` | Toplayıcı (aggregator) modül — kodu yok, bölge olarak çizilmeyecek | 1 |
| Alt POM'larda `groupId`/`version` yok | Parent'tan kalıtım | 1 |
| `${slf4j.version}` vb. `<properties>` | Özellik çözümleme | 1 |
| `dependencyManagement` + sürümsüz bağımlılık | "Yönetilen" sürüm | 1 |
| Kökteki `<dependencies>` içinde JUnit | Her modüle **kalıtılan** bağımlılık | 1 |
| `bank-api`'de `${jackson.version}` | Parent'ın özelliği çocukta kullanılıyor | 1 |
| `${project.groupId}`, `${project.version}` | Yerleşik özellikler | 1 |
| Surefire eklentisinin `<dependencies>`'i | **Proje bağımlılığı değil** — okunmamalı | 1 |
| `bank-common`'da `commons-lang3` | Beyan edilmiş, kodda kullanılmıyor | 3 |
| `bank-api` → `persistence.internal` | Beyan edilmemiş modül bağımlılığı + JPMS kaçak geçişi | 3 |
| `service.account` ↔ `service.audit` | Paket döngüsü | 3 |
| Joker/static import, tam nitelikli ad, iç içe tip, aynı paket kullanımı | Ad çözümleme | 2–3 |

**`atlas-shop-gradle` (Gradle)**

| Dosya / yer | Kenar durum |
|---|---|
| `settings.gradle.kts` | `include("a", "b")` ve ayrı satırda `include(":c")` |
| Kök `build.gradle.kts` | `subprojects { }` — dinamik yapılandırma, okunamaz → uyarı |
| `gradle/libs.versions.toml` | `module =`, `group/name =`, düz dizgi; `version.ref`; `bundles` |
| `shop-core` | `libs.junit.jupiter` — takma ad `junit-jupiter` iken erişimci noktalı |
| `shop-catalog` | `api(project(...))` ve **yorum satırına alınmış** bağımlılıklar |
| `shop-web/build.gradle` | **Groovy** DSL: parantezsiz çağrı, `libs.bundles.logging` |

Örneklerin doğru olduğunu kendi araçlarıyla da sınadık: Maven örneği `javac` ile derlendi (`bank-common`, `bank-domain`, `bank-persistence` modül yoluyla, JPMS dahil); Gradle örneği için `gradle -q projects` ve `gradle :shop-web:dependencies --configuration compileClasspath` çalıştırıldı. Gradle'ın kendi listesi ile bizim okuduğumuz liste aynı:

```
compileClasspath
+--- project :shop-catalog
|    \--- project :shop-core
+--- io.javalin:javalin:6.4.0
+--- org.slf4j:slf4j-api:2.0.16
\--- org.slf4j:slf4j-simple:2.0.16
```

### Olgular: "ne gördüm", "ne anlama geliyor" değil

PLAN 5.2'deki karar burada hayata geçiyor. Tarayıcı **yorum yapmaz**; sadece build dosyasında yazanı core'un anlayacağı düz yapılara döker:

```
pom.xml / build.gradle.kts  ──scan──▶  ProjectFacts  ──core (Faz 3)──▶  Atlas
```

Neden ayrı bir katman?

- **Test kolaylığı:** Faz 3'teki analiz testleri disk görmeden, elle kurulmuş küçük `ProjectFacts` ile koşacak.
- **Maven ile Gradle'ı eşitlemek:** İki dünyanın kavramları farklı (`<scope>test</scope>` ↔ `testImplementation`), olgular ortak bir sözlük: `Scope::Test`.
- **Bağımlılık yönü:** Olgu tipleri core'da. Core yine hiçbir şeye bağımlı değil; scan core'a bağımlı.

### Neden `quick-xml` ve neden akış (streaming)?

POM'u okumanın iki yolu var:

| Yaklaşım | Nasıl | Artı / eksi |
|---|---|---|
| **DOM** (`roxmltree` gibi) | Tüm belgeyi bellekte ağaca çevir, sonra gez | Kolay sorgu; ama bize gereken %10'luk kısım için tüm ağacı kurar |
| **Akış** (`quick-xml`) | `Start`, `Text`, `End` olaylarını sırayla al | Hızlı, az bellek; durumu (state) kendin tutarsın |

Akış yaklaşımı Rust'ta bir şey öğretiyor: **açık yığın (stack) ile durum makinesi**. Her `Start` olayında elemanın adını yığına iter, her `End`'de çekeriz. Böylece her an "neredeyim?" sorusunun cevabı yığının kendisidir: `["project", "dependencies", "dependency", "version"]`.

### Neden Gradle için bir tokenizer?

`build.gradle(.kts)` bir program. Tam okumak için Gradle'ı çalıştırmak gerekir, biz bunu yapmıyoruz (PLAN kapsam kararı). Ama düzenli ifade (regex) de yetmez:

```kotlin
// implementation("com.example:ghost:1.0")   ← yorum, okunmamalı
x = "{ not a block }"                          ← süslü parantez dizgi içinde
```

Regex bunları ayırt edemez. Çözüm: önce kaynağı **token**'lara ayırmak (tanımlayıcı, dizgi, sembol), yorumları ve dizgi içeriklerini bu aşamada etkisiz hâle getirmek. Sonra token dizisi üzerinde basit kalıplar aramak. Bir derleyicinin ilk aşamasının (lexer) minyatürü — ve Faz 2'de tree-sitter'ın bunu bizim yerimize nasıl çok daha kapsamlı yaptığını görünce farkı daha iyi anlayacağız.

## 3. Adım adım uygulama

### 3.1 Bağımlılıklar

```toml
# Cargo.toml (workspace)
[workspace.dependencies]
quick-xml = "0.42"
toml = "1"
ignore = "0.4"
insta = "1"
```

```toml
# crates/java-atlas-scan/Cargo.toml
[dependencies]
java-atlas-core.workspace = true
thiserror.workspace = true
tree-sitter.workspace = true
tree-sitter-java.workspace = true
quick-xml.workspace = true
toml.workspace = true
ignore.workspace = true

[dev-dependencies]
insta.workspace = true
```

Dosya düzeni:

```
crates/java-atlas-core/src/
├── lib.rs
└── facts.rs                 ← yeni
crates/java-atlas-scan/
├── src/
│   ├── lib.rs               ← ScanError genişledi, read_build dışa açıldı
│   ├── paths.rs             ← sözcüksel yol normalleştirme
│   └── build_files/         ← "build/" değil: kök .gitignore build/ klasörlerini dışlıyor
│       ├── mod.rs           ← detect + read_build
│       ├── maven.rs
│       └── gradle/
│           ├── mod.rs       ← settings + script okuma
│           ├── lexer.rs
│           └── catalog.rs
├── examples/inspect.rs
└── tests/
    ├── samples.rs
    └── snapshots/*.snap
```

### 3.2 Olgu tipleri — `java-atlas-core/src/facts.rs`

```rust
pub enum BuildSystem { Maven, Gradle, Plain }

pub enum Scope { Compile, Provided, Runtime, Test }

pub enum VersionSource {
    Explicit,               // yanında yazıyor
    Property(String),       // ${jackson.version}
    Managed,                // dependencyManagement'tan
    Catalog(String),        // libs.junit.jupiter
    Unresolved,
}

pub struct ExternalDep {
    pub group: String,
    pub artifact: String,
    pub version: Option<String>,
    pub version_source: VersionSource,
    pub scope: Scope,
    pub inherited: bool,    // parent POM'dan mı geldi?
}

pub struct ModuleDep {
    pub target: String,     // hedef modülün id'si
    pub scope: Scope,
    pub exposed: bool,      // Gradle api(...)
}

pub struct ModuleFacts {
    pub id: String,         // Maven "group:artifact", Gradle ":path"
    pub name: String,
    pub path: String,       // köke göre, "/" ayraçlı
    pub build_file: String,
    pub aggregator: bool,
    pub module_deps: Vec<ModuleDep>,
    pub external_deps: Vec<ExternalDep>,
}

pub struct ScanWarning { pub file: String, pub message: String }

pub struct ProjectFacts {
    pub name: String,
    pub build_system: BuildSystem,
    pub modules: Vec<ModuleFacts>,
    pub warnings: Vec<ScanWarning>,
}
```

İki tasarım notu:

- **`VersionSource` bir enum, `String` değil.** "Bu sürüm neden 2.0.16?" sorusu ayrıntı panelinin (Faz 7) cevaplayacağı sorulardan biri. Bilgiyi şimdi kaybedersek sonra geri getiremeyiz.
- **Uyarılar hata değil.** Tek bir anlaşılmayan Gradle satırı yüzünden tüm haritayı çizmemek yanlış olur. `ScanError` yalnızca taramayı anlamsız kılan durumlar için (dosya okunamıyor, XML bozuk). PLAN'daki "kısmen okundu" uyarısının altyapısı bu.

`ProjectFacts::describe()` olguları düz metin ağaç olarak yazar. Hem `inspect` örneği hem snapshot testleri bunu kullanır. `write!` makrosunun `String`'e yazarken hata vermeyeceğini bildiğimiz için sonucu `let _ =` ile bilerek yok sayıyoruz.

### 3.3 Hangi build sistemi? — `build_files/mod.rs`

```rust
pub fn detect(root: &Path) -> Option<BuildSystem> {
    if root.join("pom.xml").is_file() {
        return Some(BuildSystem::Maven);
    }
    let gradle_files = ["settings.gradle.kts", "settings.gradle", "build.gradle.kts", "build.gradle"];
    if gradle_files.iter().any(|f| root.join(f).is_file()) {
        return Some(BuildSystem::Gradle);
    }
    has_java_files(root).then_some(BuildSystem::Plain)
}

fn has_java_files(root: &Path) -> bool {
    ignore::WalkBuilder::new(root)
        .build()
        .filter_map(Result::ok)
        .any(|entry| entry.path().extension().is_some_and(|ext| ext == "java"))
}
```

- `ignore::WalkBuilder` `.gitignore`'a saygı duyar; `target/`, `build/` gibi klasörlere girmez. Faz 2'de tüm kaynak dosyaları bununla toplayacağız.
- `.any(...)` **tembeldir**: ilk `.java` dosyasında durur. Binlerce dosyalık bir klasörde hepsini gezmeyiz.
- `bool::then_some` — `if x { Some(v) } else { None }`'nin kısa hâli.

Build dosyası olmayan ama `.java` içeren klasörler **Plain** sayılır: tek modül, bağımlılık yok. Hiçbiri yoksa `ScanError::NotAJavaProject` — MVP kabul kriterindeki "anlaşılır mesaj".

### 3.4 Maven, adım 1: tek bir dosya — `parse_pom`

`parse_pom` saf bir fonksiyon: `&str` alır, `RawPom` döner. Kalıtım yok, `${...}` çözümleme yok; **dosyada yazan, olduğu gibi**.

```rust
pub(crate) fn parse_pom(xml: &str) -> Result<RawPom, String> {
    let mut reader = Reader::from_str(xml);
    let mut stack: Vec<String> = Vec::new();
    let mut text = String::new();
    let mut pom = RawPom::default();
    let mut dep: Option<RawDep> = None;

    loop {
        let event = reader
            .read_event()
            .map_err(|e| format!("XML error at byte {}: {e}", reader.error_position()))?;
        match event {
            Event::Start(e) => {
                stack.push(e.local_name().as_ref().to_owned());
                text.clear();
                open(&stack, &mut pom, &mut dep);
            }
            Event::Empty(e) => {               // <relativePath/>
                stack.push(e.local_name().as_ref().to_owned());
                text.clear();
                open(&stack, &mut pom, &mut dep);
                close(&stack, "", &mut pom, &mut dep);
                stack.pop();
            }
            Event::Text(t) => text.push_str(&t.xml10_content()),
            Event::CData(c) => text.push_str(&c.xml10_content()),
            Event::GeneralRef(r) => { /* &amp; &lt; &#45; … */ }
            Event::End(_) => {
                close(&stack, text.trim(), &mut pom, &mut dep);
                stack.pop();
                text.clear();
            }
            Event::Eof => break,
            _ => {}
        }
    }
    Ok(pom)
}
```

Asıl iş `close`'ta, ve Rust'ın **dilim kalıpları (slice patterns)** burada parlıyor:

```rust
fn close(stack: &[String], text: &str, pom: &mut RawPom, dep: &mut Option<RawDep>) {
    let path: Vec<&str> = stack.iter().map(String::as_str).collect();
    let value = || (!text.is_empty()).then(|| text.to_owned());

    match path.as_slice() {
        ["project", "groupId"] => pom.group = value(),
        ["project", "artifactId"] => pom.artifact = value(),
        ["project", "parent", field] => { /* groupId, artifactId, version, relativePath */ }
        ["project", "properties", name] => pom.properties.push(((*name).to_owned(), text.to_owned())),
        ["project", "modules", "module"] => { /* … */ }

        p if p == DEPENDENCY => { /* <dependency> bitti: listeye ekle */ }
        p if p == MANAGED_DEPENDENCY => { /* … */ }

        [parent @ .., field] if parent == DEPENDENCY || parent == MANAGED_DEPENDENCY => {
            // groupId / artifactId / version / scope
        }
        _ => {}
    }
}
```

- `["project", "properties", name]` — üçüncü eleman **ne olursa olsun** `name`'e bağlanır. `<slf4j.version>` gibi önceden bilinmeyen özellik adları tek satırda yakalanır.
- `[parent @ .., field]` — "son eleman `field`, öncesi `parent`". Bağımlılık alanlarını hem normal listede hem `dependencyManagement`'ta tek kolda okuyoruz.
- **Eklenti bağımlılıkları** (`project/build/plugins/plugin/dependencies/dependency`) hiçbir kola uymaz → kendiliğinden yok sayılır. Bunun için tek satır kod yazmadık; yolun **tam** eşleşmesi yeterli.
- `local_name()` — `<pom:project>` gibi ön ekli belgelerde de çalışsın diye ad alanı önekini atar.

`quick-xml` 0.42 notu: metin olayları artık `Cow<str>` taşıyor; eski sürümlerdeki `unescape()` yerine `xml10_content()` var ve `&amp;` gibi varlıklar **ayrı bir `GeneralRef` olayı** olarak geliyor. İnternetteki eski örnekler bu yüzden derlenmez.

### 3.5 Maven, adım 2: kalıtım ve etkin model

Maven'da bir modülün gerçek hâli, kendi POM'u ile parent zincirinin birleşimidir ("effective POM"). `MavenReader::effective` bunu modül başına bir kez hesaplar ve `Rc<Effective>` olarak önbellekte tutar:

```rust
fn effective(&mut self, pom_path: &Path) -> Result<Rc<Effective>, ScanError> {
    let pom_path = normalize(pom_path);
    if let Some(done) = self.cache.get(&pom_path) {
        return Ok(Rc::clone(done));
    }
    if !self.in_progress.insert(pom_path.clone()) {
        return Err(/* parent zinciri döngü oluşturuyor */);
    }
    let raw = self.read_raw(&pom_path)?;
    let parent = self.load_parent(&pom_path, &raw)?;   // özyinelemeli: effective(parent)
    let effective = Rc::new(merge(raw, parent.as_deref(), &pom_path, self));
    self.in_progress.remove(&pom_path);
    self.cache.insert(pom_path, Rc::clone(&effective));
    Ok(effective)
}
```

- **Neden `Rc`?** Beş modül aynı parent'ı paylaşıyor. Parent'ın etkin modelini beş kez kopyalamak yerine sayaçlı bir referans paylaşıyoruz. Tek iş parçacığında çalıştığımız için `Arc` gerekmiyor.
- **`in_progress` kümesi:** A'nın parent'ı B, B'nin parent'ı A olursa özyineleme sonsuza gider. Maven bunu reddeder; biz de anlamlı bir hata veriyoruz.

`load_parent`, `<relativePath>` kurallarını uygular:

| Yazılan | Anlamı |
|---|---|
| (yok) | `../pom.xml` |
| `<relativePath/>` | Diske bakma (parent bir depodan gelir) |
| `../parent` | O klasördeki `pom.xml` |
| `../parent/pom.xml` | O dosya |

Bulunan dosyanın `artifactId`'si `<parent>`'ta yazanla aynı değilse onu parent saymayız. Proje dışındaki parent'lar (ör. Spring Boot) için bir uyarı üretilir ve sürümü oradan gelen bağımlılıklar `Unresolved` kalır.

`merge` kalıtım kurallarını uygular:

| Alan | Kural |
|---|---|
| `groupId`, `version` | Yoksa parent'tan |
| `packaging` | **Kalıtılmaz** (varsayılan `jar`) |
| `<properties>` | Parent'ınkiler + kendininkiler (çocuk ezer) + yerleşikler (`project.version` …) |
| `dependencyManagement` | Parent'ınki + kendininki |
| `<dependencies>` | Parent'ınkiler (`inherited = true`) + kendininkiler; aynı `g:a` ise çocuğunki kazanır |

### 3.6 `${...}` çözümleme

```rust
pub(crate) fn interpolate(raw: &str, props: &HashMap<String, String>) -> Interpolated
```

Üç kenar durum, üç test:

```rust
// Özellik başka özelliğe başvurabilir
let p = props(&[("a", "${b}-x"), ("b", "1.0")]);
assert_eq!(interpolate("v${a}", &p).value, "v1.0-x");

// Bilinmeyen özellik raporlanır ve olduğu gibi bırakılır
assert_eq!(interpolate("${nope}", &HashMap::new()).unresolved, vec!["nope"]);

// Kendine başvuran özellik sonsuz döngüye girmez (MAX_DEPTH = 8)
let p = props(&[("a", "${a}")]);
assert_eq!(interpolate("${a}", &p).unresolved, vec!["a"]);
```

`first_property` alanı sürüm kaynağını raporlamak için: `${jackson.version}` → `VersionSource::Property("jackson.version")`.

### 3.7 Modül mü, dış kütüphane mi?

Tüm modüllerin `group:artifact` çiftleri bir kümeye konur. Bir bağımlılık bu kümedeyse `ModuleDep`, değilse `ExternalDep` olur. `bank-domain`'in `${project.groupId}:bank-common` bağımlılığı çözümlendikten sonra `com.atlasbank:bank-common` olur ve modül bağımlılığı olarak tanınır.

### 3.8 Gradle, adım 1: lexer — `gradle/lexer.rs`

```rust
pub(crate) enum Tok {
    Ident(String),   // implementation, libs, java-library (backtick'ten)
    Str(String),     // dizginin içeriği, tırnaksız
    Sym(char),       // ( ) { } . : , =
    Other,           // sayılar vb.
}

pub(crate) struct Token { pub tok: Tok, pub line: u32 }
```

Lexer `Vec<char>` üzerinde elle yazılmış bir döngü. Tanıdığı şeyler:

- `//` ve `/* */` yorumları — **atlanır**
- `"…"`, `'…'`, `"""…"""`, `'''…'''` — içerik tek bir `Str` olur; içindeki `{`, `//` etkisiz
- `` `java-library` `` — Kotlin backtick tanımlayıcısı tek `Ident`
- satır sayacı — uyarılarda "line 12" diyebilmek için

### 3.9 Gradle, adım 2: token'lardan bildirimler

```rust
pub(crate) enum Notation {
    Coordinates(String),   // "g:a:v"
    Project(String),       // project(":core")
    Catalog(String),       // libs.jackson.databind
    Bundle(String),        // libs.bundles.logging
    Ignored(&'static str), // platform(...), files(...)
}
```

`declarations` yalnızca **en üst seviyedeki** `dependencies { }` bloklarını okur (süslü parantez derinliği 0). Böylece:

```kotlin
subprojects { dependencies { implementation("ghost:ghost:1") } }   // okunmaz
dependencies { implementation("real:real:1") }                      // okunur
```

Blok içinde her `configuration` adından sonra `notation` çağrılır. Desteklenen biçimler:

| Biçim | Örnek |
|---|---|
| Kotlin, parantezli | `implementation("a:b:1")` |
| Groovy, parantezsiz | `implementation 'a:b:1'` |
| Çoklu | `implementation 'x:y:1', 'z:w:2'` |
| Proje | `project(":core")`, `project(path = ":util")`, `project ':core'` |
| Katalog | `libs.junit.jupiter`, `libs.bundles.logging` |
| Groovy map | `group: 'g', name: 'n', version: '2'` |
| Sarmalayıcılar | `testFixtures(project(":x"))`; `platform(...)` ve `files(...)` bilerek yok sayılır |

Anlaşılmayan bir bildirim (`implementation(myLib)` gibi bir değişken) satır numarasıyla **uyarı** olur.

Yapılandırma adları ortak kapsama eşlenir:

```rust
fn configuration(name: &str) -> Option<(Scope, bool)> {
    Some(match name {
        "implementation" | "compile" => (Scope::Compile, false),
        "api" => (Scope::Compile, true),            // tüketicilere sızar
        "compileOnly" | "annotationProcessor" => (Scope::Provided, false),
        "compileOnlyApi" => (Scope::Provided, true),
        "runtimeOnly" | "runtime" => (Scope::Runtime, false),
        "testImplementation" | /* … */ => (Scope::Test, false),
        _ => return None,
    })
}
```

`api` ile `implementation` farkı haritada önemli: `shop-catalog`, `shop-core`'u `api` ile istediği için `shop-web` de `Sku` sınıfını görebiliyor. Faz 3'te "beyan edilmemiş bağımlılık" kontrolü bu bilgiyi kullanacak (`exposed`).

### 3.10 Gradle, adım 3: sürüm kataloğu — `gradle/catalog.rs`

`toml` crate'i dosyayı `toml::Table`'a çevirir; gerisi `Value` enum'u üzerinde desen eşleme:

```rust
fn library_of(value: &Value, versions: &Table) -> Option<Library> {
    match value {
        Value::String(s) => { /* "g:a:v" */ }
        Value::Table(t) => {
            // module = "g:a"  ya da  group = "g", name = "a"
            // version = "1" | version.ref = "x" | version = { strictly = "1" }
        }
        _ => None,
    }
}
```

TOML'un küçük bir sürprizi: `version.ref = "junit"` **noktalı anahtar**dır, yani `version = { ref = "junit" }` ile aynı şeydir. Bu yüzden `version_of` tek bir fonksiyonla hem düz dizgiyi hem tabloyu çözer.

Erişimci adı kuralı: takma addaki `-`, `_` ve `.` karakterlerinin hepsi noktaya dönüşür:

```rust
pub(crate) fn accessor(alias: &str) -> String {
    alias.replace(['-', '_'], ".")
}
```

`slf4j_simple` → `libs.slf4j.simple`, `junit-jupiter` → `libs.junit.jupiter`.

### 3.11 Gradle'ı birleştirmek

`gradle::read`:

1. `settings.gradle(.kts)` → kök adı ve `include` listesi (`:a:b` → `a/b` klasörü)
2. `gradle/libs.versions.toml` varsa katalog
3. Projeler: kök (`:`) + her `include`
4. Her proje için `build.gradle.kts`, yoksa `build.gradle`; bildirimleri olgulara çevir
5. Kökte `subprojects`/`allprojects` geçiyorsa uyarı

Kök proje, alt projeleri varsa ve kendi `src/` klasörü yoksa **toplayıcı** sayılır.

### 3.12 Yol yardımcıları — `paths.rs`

`fs::canonicalize` Windows'ta `\\?\C:\…` biçiminde yollar döndürür ve sembolik bağları çözer; ikisi de göreli yol hesabını şaşırtır. Biz yalnızca **sözcüksel** (lexical) normalleştirme yapıyoruz: `.` silinir, `..` bir önceki parçayı katlar. Olgulardaki tüm yollar her platformda `/` ayraçlıdır — snapshot'lar Windows ve Linux'ta aynı çıksın diye.

### 3.13 Testler

| Dosya | Ne sınıyor |
|---|---|
| `maven.rs` (birim) | Yazılanı okumak, boş `relativePath`, varlıklar, bozuk XML, özellik çözümleme |
| `gradle/lexer.rs` | Kotlin/Groovy çağrıları, yorumlar, dizgi içi süslü parantez, backtick, satır sayısı |
| `gradle/catalog.rs` | Tüm katalog biçimleri, bundle'lar, bozuk TOML |
| `gradle/mod.rs` | Bildirim biçimleri, iç içe `dependencies`, closure/platform atlama, settings |
| `tests/samples.rs` | İki örnek projenin uçtan uca okunuşu + snapshot |

Snapshot testleri `insta` ile:

```rust
#[test]
fn atlas_bank_snapshot() {
    let facts = read_build(&sample("atlas-bank")).unwrap();
    insta::assert_snapshot!(facts.describe());
}
```

İlk çalıştırmada snapshot dosyası oluşur (`tests/snapshots/samples__atlas_bank_snapshot.snap`). Sonraki her çalıştırmada çıktı bununla karşılaştırılır. Bilerek bir değişiklik yaptığında:

```bash
cargo install cargo-insta    # bir kez
cargo insta review           # farkları gör, kabul et
```

Snapshot'ın yanında **anlamlı iddialar** da var (`atlas_bank_versions_come_from_the_right_place` gibi). Snapshot "bir şey değişti" der; iddia "bu davranış önemli" der. İkisi birlikte.

### 3.14 Çalıştır

```bash
cargo test --workspace
cargo run -p java-atlas-scan --example inspect -- samples/atlas-bank
cargo run -p java-atlas-scan --example inspect -- samples/atlas-shop-gradle
# kendi projelerinden biriyle:
cargo run -p java-atlas-scan --example inspect -- /yol/bir/java/projesi
```

## 4. Takıldığın yerler

| Belirti | Neden | Çözüm |
|---|---|---|
| `no method named unescape` / `map_err` on `Cow` | `quick-xml` 0.38+ API değişikliği | `xml10_content()` kullan; varlıklar `Event::GeneralRef` ile gelir |
| Eklenti bağımlılıkları da listeye giriyor | Bağımlılık yolu "sonu `dependency` ile biten her şey" diye eşleniyor | Tam yol eşle: `["project", "dependencies", "dependency"]` |
| Bütün modüllerde JUnit iki kez | Kalıtılan ile çocuğun aynı bağımlılığı birlikte eklenmiş | `merge`'de aynı `g:a` için çocuğunki kalıtılanı siler (`retain`) |
| `libs.junit.jupiter` "katalogda yok" | Takma ad `junit-jupiter` olarak aranıyor | Anahtarları `accessor()` ile normalleştir |
| `version.ref` bulunamıyor | Noktalı TOML anahtarını düz anahtar sanmak | `t.get("version")` bir tablo döner; içinde `ref` ara |
| `src/build/` altındaki dosyalar commit'e girmedi | `coding-vibes` kök `.gitignore`'u `**/build/` kalıbıyla tüm `build` klasörlerini dışlıyor | Modül klasörünün adı `build_files`; `git check-ignore -v <dosya>` hangi kuralın devrede olduğunu söyler |
| Windows'ta snapshot farkı `\` ↔ `/` | Yollar platform ayracıyla yazılmış | `relative_slash` ile her yerde `/` |
| Proje dışı parent'ta yüzlerce "no version" uyarısı | Spring Boot gibi parent'ların yönettiği sürümler | Parent dışarıdaysa tek bir uyarı, bağımlılık başına uyarı yok |

## 5. Ne öğrendik

- **Akış tabanlı XML okuma:** olay döngüsü, açık yığın ile konum takibi.
- **Dilim kalıpları:** `["project", "properties", name]`, `[parent @ .., field]` — iç içe yapıları okumanın Rust'a özgü zarif yolu.
- **Kalıtım modeli:** `Rc` ile paylaşılan, önbellekli, döngü korumalı özyinelemeli çözümleme.
- **Özyinelemeli metin genişletme** ve derinlik sınırıyla sonsuz döngüden korunma.
- **El yazımı lexer:** yorum ve dizgilerin neden regex'ten önce ayıklanması gerektiği.
- **`toml::Value`** üzerinde desen eşleme; TOML'un noktalı anahtarları.
- **Hata ile uyarı ayrımı:** kısmi başarıyı modellemek.
- **Snapshot testing** ile anlamlı iddiaları birlikte kullanmak.

## 6. Kendin dene

1. **Profiller:** `atlas-bank` kök POM'una `<profiles><profile><dependencies>…</dependencies></profile></profiles>` ekle. Bugün bu bağımlılıklar yok sayılıyor. Bir profildeki bağımlılıkları uyarı olarak raporla ("profile 'x' declares 2 dependencies; profiles are not evaluated").
2. **Tip güvenli proje erişimcileri:** Gradle 8'de `implementation(projects.shopCore)` yazılabiliyor. `notation`'a `projects.` desteği ekle. İpucu: `shopCore` → `:shop-core` dönüşümü için `known` listesindeki adları camelCase'e çevirip karşılaştır.
3. **`inspect --json`:** Örneğe bir bayrak ekleyip olguları JSON olarak yazdır. Core'a `serde` eklemeden yapmanın yolu ne olabilir? (İpucu: Faz 4'teki DTO'lar.)
