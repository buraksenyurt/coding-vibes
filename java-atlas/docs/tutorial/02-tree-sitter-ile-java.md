# Faz 2 — tree-sitter ile Java Kaynaklarını Okumak

> **Önceki:** [01 — Build dosyalarını okumak](01-build-dosyalari.md) · **Sonraki:** [03 — Graf ve analiz](03-graf-ve-analiz.md)

## 1. Bu bölümde ne yapacağız

Faz 1'de bölgeleri (modülleri) ve adaları (dış kütüphaneleri) bulduk. Şimdi **şehirlere ve binalara**, yani paketlere ve tiplere iniyoruz. Her `.java` dosyasını tree-sitter ile ayrıştırıp şunları çıkaracağız:

- `package` bildirimi
- `import`'lar (tekli, joker `.*`, `static`)
- tip bildirimleri (class, interface, enum, record, annotation; iç içe olanlar dahil) ve satır sayıları
- gövdede geçen **tip adı adayları** (Faz 3'te çözümlenecek)
- `module-info.java`: `requires`, `exports`, `opens`, `uses`, `provides`

Bölümün sonunda:

```bash
cargo run -p java-atlas-scan --example inspect -- samples/atlas-bank --sources
```

```
com.atlasbank:bank-domain · 4 file(s)
  module com.atlasbank.domain  <bank-domain/src/main/java/module-info.java>
    requires transitive com.atlasbank.common
    exports com.atlasbank.domain.model
    exports com.atlasbank.domain.rules
  bank-domain/src/main/java/com/atlasbank/domain/model/Account.java
    package com.atlasbank.domain.model · 33 lines
    public class Account  L5 +29
    public enum Account.Status  L8 +1
    import com.atlasbank.common.Money  L3
    uses Customer×3 Money×3 Status×4 String×3 Transaction
...
17 file(s) in 5ms
```

Tarama `rayon` ile paraleldir. Gerçek projelerde denedik: **Apache Commons Lang** (628 dosya) ~0,45 sn, **Guava** (4 modül, 1.556 dosya) ~0,85 sn — 2 çekirdekli bir bulut makinesinde, release derlemesiyle. Tek iş parçacığıyla Guava ~2 sn sürüyor.

## 2. Neden böyle

### Somut sözdizimi ağacı (CST)

Faz 0'da tree-sitter ile tanışmıştık. tree-sitter bir **somut** sözdizimi ağacı üretir: kaynak koddaki her şey (`;`, `(`, `static` anahtar sözcüğü) ağaçta bir düğümdür. Bu düğümlerin bir kısmı **isimli** (`class_declaration`, `identifier`), bir kısmı **isimsiz**dir (`;`, `static`). Ağaçta gezinirken iki ayrı API var:

| API | Ne döner |
|---|---|
| `children()` | İsimli + isimsiz tüm çocuklar |
| `named_children()` | Sadece isimli çocuklar |
| `child_by_field_name("name")` | Gramerde **alan adı** verilmiş çocuk |

Bir düğümün yapısını tahmin etmek yerine **bakmak** en doğrusu. Bunun için `/tmp`'de küçük bir döküm aracı yazdık (Bölüm 4'te). Örneğin:

```java
import static x.Y.z;
import c.d.*;
```

```
import_declaration
  scoped_identifier            ← x.Y.z
    scope: scoped_identifier
      scope: identifier "x"
      name: identifier "Y"
    name: identifier "z"
import_declaration
  scoped_identifier            ← c.d
  asterisk                     ← isimli düğüm!
```

`static` ağaçta isimsiz bir token olarak durur; `*` ise `asterisk` adında **isimli** bir düğüm. İkisini de `children()` ile buluyoruz.

### İki gezinme stili

1. **Yapısal gezinme** — bildirimler (package, import, tipler) için. Nerede olduklarını biliyoruz: kök düğümün doğrudan çocukları ve sınıf gövdeleri. Alan adlarıyla (`name`, `body`, `type_parameters`) doğrudan hedefe gidiyoruz.
2. **Tam dolaşma** — referanslar için. Bir tip adı neredeyse her yerde geçebilir: alan tipi, generic argüman, cast, lambda, `instanceof`, `throws`… Burada tüm ağacı dolaşıp ilgilendiğimiz düğüm türlerini topluyoruz.

### "Tip adı adayı" ne demek?

Ayrıştırıcı bir **sözdizimi** aracıdır, anlam bilmez. `Money.zero()` ifadesinde `Money` ağaçta `type_identifier` değil, sıradan bir `identifier`'dır — tree-sitter onun bir sınıf mı yoksa bir değişken mi olduğunu bilemez:

```
method_invocation
  object: identifier "Money"
  name: identifier "zero"
```

Java'nın adlandırma kuralı (tipler büyük harfle başlar) burada güçlü bir ipucu. Bu yüzden `object` konumundaki büyük harfli tanımlayıcıları da **aday** olarak topluyoruz. Bunun bedeli: `LOG.info(...)` içindeki `LOG` de aday olur. Faz 3'te her aday sembol tablosu, import'lar ve `java.lang` ile eşleştirilecek; eşleşmeyen `LOG` sessizce düşecek. Faz 2'nin görevi **hiçbir gerçek kullanımı kaçırmamak**, ayıklamak Faz 3'ün işi.

Topladığımız aday türleri:

| Düğüm | Örnek | Not |
|---|---|---|
| `type_identifier` | `Money m;`, `List<Account>`, `extends Base` | `var` ve generic parametreler (`T`, `R`) hariç |
| `scoped_type_identifier` | `java.util.concurrent.atomic.AtomicLong` | Bütün olarak alınır, içine inilmez |
| `method_invocation.object` | `Money.zero()`, `LoggerFactory.getLogger(...)` | Büyük harfle başlayan `identifier` |
| `field_access.object` | `BigDecimal.ZERO`, `Status.OPEN` | Aynı kural |
| `method_reference` | `AccountSummary::of` | İlk çocuk |
| `marker_annotation`, `annotation` | `@Override`, `@Test` | `name` alanı |

### Neden `rayon` ve neden `map_init`?

Her dosya diğerlerinden bağımsız ayrıştırılır: klasik **veri paralelliği**. `rayon` ile seri bir `iter()`'i paralel `par_iter()`'e çevirmek tek kelimelik bir değişiklik — ama bir engel var: tree-sitter'ın `Parser`'ı iş parçacıkları arasında paylaşılamaz (`Sync` değil) ve dosya başına yeni bir `Parser` kurmak israf.

`map_init` tam bunun için:

```rust
jobs.par_iter()
    .map_init(
        || java_parser().expect("grammar was loaded above"),  // iş parçacığı başına BİR kez
        |parser, job| parse_one(parser, root, job),            // her dosyada aynı parser
    )
    .collect()
```

İlk kapanış (closure) her işçi iş parçacığı için bir `Parser` üretir; ikincisi o parser'ı `&mut` olarak alır. `collect()` **sırayı korur**: sonuçlar `jobs` ile aynı sırada gelir, bu da snapshot'ların her makinede aynı çıkmasını sağlar.

## 3. Adım adım uygulama

### 3.1 Bağımlılıklar

```toml
# Cargo.toml (workspace)
rayon = "1"
streaming-iterator = "0.1"
```

```toml
# crates/java-atlas-scan/Cargo.toml
[dependencies]
rayon.workspace = true

[dev-dependencies]
streaming-iterator.workspace = true   # sadece query karşılaştırma testi için
```

Yeni dosyalar:

```
crates/java-atlas-scan/src/
├── lib.rs                   ← read_project, read_sources dışa açıldı
└── sources/
    ├── mod.rs               ← iş listesi, paralel ayrıştırma, sonuçları dağıtma
    └── java.rs              ← tek dosya → olgular; module-info → JPMS olguları
```

Faz 0'daki `declared_package` duman testi artık gereksiz; yerini `java.rs` aldı ve silindi. `ScanError::NoTree` de kalktı: ağaç üretilemeyen tek bir dosya artık bir **uyarı**, tüm taramayı durdurmuyor.

### 3.2 Olgu tipleri — `facts.rs`'e eklenenler

```rust
pub struct ModuleFacts {
    // … Faz 1 alanları …
    pub source_roots: Vec<SourceRoot>,   // boş = src/main/java + src/test/java
    pub jpms: Option<JpmsFacts>,
    pub files: Vec<SourceFileFacts>,
}

pub enum TypeKind { Class, Interface, Enum, Record, Annotation }

pub struct TypeFacts {
    pub name: String,      // iç içe tiplerde "Outer.Inner"
    pub kind: TypeKind,
    pub is_public: bool,
    pub line: u32,
    pub lines: u32,        // binanın boyutu
}

pub struct ImportFacts {
    pub path: String,      // "com.x.Money", "com.x.Money.zero", "com.x"
    pub is_static: bool,
    pub on_demand: bool,   // .*
    pub line: u32,
}

pub struct NameRef {
    pub name: String,      // "Account", "Account.Status", "java.util.List"
    pub line: u32,         // ilk geçtiği satır → Faz 7'deki kanıt listesi
    pub count: u32,        // kaç kez → yol kalınlığı
}

pub struct SourceFileFacts {
    pub path: String,
    pub package: String,
    pub is_test: bool,
    pub lines: u32,
    pub imports: Vec<ImportFacts>,
    pub types: Vec<TypeFacts>,
    pub references: Vec<NameRef>,
    pub has_errors: bool,
}

pub struct JpmsFacts {
    pub name: String,
    pub open: bool,
    pub file: String,
    pub requires: Vec<Requires>,        // { module, transitive, is_static }
    pub exports: Vec<PackageGrant>,     // { package, to: Vec<String> }
    pub opens: Vec<PackageGrant>,
    pub uses: Vec<String>,
    pub provides: Vec<Provides>,        // { service, with: Vec<String> }
}
```

`NameRef`'in `line` ve `count` alanları ileriye dönük: `count` paketler arası yolun kalınlığına, `line` ise ayrıntı panelindeki "bu bağımlılık nereden geliyor?" sorusuna (`AccountController.java:3`) dönüşecek.

`ProjectFacts::describe_sources()` bu olguları snapshot'a uygun düz metne döker.

### 3.3 Paket, import ve tipler — yapısal gezinme

```rust
pub(crate) fn file_facts(tree: &Tree, src: &str, path: String, is_test: bool) -> SourceFileFacts {
    let root = tree.root_node();
    let mut facts = SourceFileFacts { /* … */ has_errors: root.has_error(), .. };
    let mut type_params = HashSet::new();

    let mut cursor = root.walk();
    for node in root.named_children(&mut cursor) {
        match node.kind() {
            "package_declaration" => { /* identifier | scoped_identifier çocuğu */ }
            "import_declaration" => facts.imports.push(import(node, src)),
            kind if type_kind(kind).is_some() => {
                collect_types(node, src, "", &mut facts.types, &mut type_params);
            }
            _ => {}
        }
    }
    facts.references = references(root, src, &type_params);
    facts
}
```

`collect_types` özyinelemeli: bir tip bildirimini kaydeder, `body` alanına iner ve oradaki tip bildirimleri için kendini `"Outer"` önekiyle çağırır. Tek tuzak `enum`: üyeleri bir seviye daha derinde, `enum_body_declarations` içinde durur:

```java
enum Status { A, B;          // ← enum_body
    interface Deep {}        // ← enum_body > enum_body_declarations > interface_declaration
}
```

Generic parametreler (`class A<T>`, `<R> R map(...)`) aynı geçişte `type_params` kümesine toplanır, çünkü ağaçta `T` de bir `type_identifier`'dır ve referanslardan ayıklanması gerekir.

> **Bilinen sınır:** Küme dosya genelinde tutuluyor, kapsam (scope) takibi yok. Bir sınıfın tip parametresi `T` ile aynı dosyada gerçekten `T` adında bir sınıf varsa kullanımı kaybolur. Pratikte çok nadir.

`is_public` için `modifiers` düğümünün içinde isimsiz `public` token'ını arıyoruz:

```rust
fn is_public(decl: Node) -> bool {
    let mut cursor = decl.walk();
    decl.children(&mut cursor)
        .filter(|c| c.kind() == "modifiers")
        .any(|m| {
            let mut inner = m.walk();
            m.children(&mut inner).any(|t| t.kind() == "public")
        })
}
```

### 3.4 Referanslar — özyinelemesiz dolaşma

```rust
let mut cursor = root.walk();
'walk: loop {
    let node = cursor.node();
    let mut descend = true;

    match node.kind() {
        "package_declaration" | "import_declaration" => descend = false,
        "scoped_type_identifier" => { add(compact(text(node, src)), line(node)); descend = false; }
        "type_identifier" => { /* var ve generic parametreler hariç */ }
        "method_invocation" | "field_access" => { /* büyük harfli object */ }
        "method_reference" => { /* ilk çocuk */ }
        "marker_annotation" | "annotation" => { /* name alanı */ }
        _ => {}
    }

    if descend && cursor.goto_first_child() {
        continue;
    }
    loop {
        if cursor.goto_next_sibling() {
            continue 'walk;
        }
        if !cursor.goto_parent() {
            break 'walk;
        }
    }
}
```

Neden özyineleme değil de `TreeCursor` ile döngü?

- **Yığın taşması yok.** Üretilmiş kodlarda (parser üreteçleri, büyük `switch`'ler, uzun `+` zincirleri) ağaç binlerce seviye derin olabilir. Özyinelemeli bir fonksiyon her seviyede bir çağrı çerçevesi harcar; cursor tek bir nesneyle gezinir.
- **Tahsis yok.** `children()` her çağrıda bir iterator kurar; cursor aynı nesneyi yukarı-aşağı oynatır.
- **Etiketli döngüler** (`'walk:`) burada okunurluğu kurtarıyor: içteki döngüden dıştakini doğrudan sürdürebiliyoruz (`continue 'walk`) ya da bitirebiliyoruz (`break 'walk`).

`scoped_type_identifier`'a girmememizin nedeni ağaçtaki yapısı:

```
scoped_type_identifier                 ← java.util.concurrent.atomic.AtomicLong
  scoped_type_identifier               ← java.util.concurrent.atomic
    scoped_type_identifier             ← java.util.concurrent
      scoped_type_identifier           ← java.util
        type_identifier "java"
        type_identifier "util"
      ...
```

İçine inseydik `java`, `util`, `concurrent` da birer "tip" olarak toplanırdı.

Sayaçları bir `BTreeMap<String, (u32, u32)>` tutuyor: ad → (ilk satır, adet). `BTreeMap` seçimi bilinçli — anahtarlar sıralı gelir, snapshot'lar kararlı olur.

### 3.5 `module-info.java`

```
module_declaration
  name: scoped_identifier
  body: module_body
    requires_module_directive
      modifiers: requires_modifier     ← "transitive" / "static" (birden fazla olabilir)
      module: scoped_identifier
    exports_module_directive
      package: scoped_identifier
      modules: identifier              ← "to" listesi, aynı alan adıyla tekrar eder
    provides_module_directive
      provided: scoped_identifier
      scoped_identifier                ← "with" listesi, ALAN ADI YOK
```

Aynı alan adıyla tekrar eden çocuklar için `children_by_field_name`:

```rust
let fields = |name: &str| -> Vec<String> {
    let mut c = directive.walk();
    directive.children_by_field_name(name, &mut c)
        .map(|n| compact(text(n, src)))
        .collect()
};
```

`provides … with A, B` listesinin alan adı olmadığı için orada farklı bir yol izliyoruz: tüm isimli çocukları alıp ilkini (servis) ayırıyoruz — `split_first()`.

`module-info.java` sıradan bir dosya olarak `files` listesine **girmez**; sadece modülün `jpms` alanını doldurur. Test kaynaklarında da bir `module-info.java` varsa ana kaynaktaki kazanır.

### 3.6 Paralel tarama — `sources/mod.rs`

Üç adım:

```rust
pub fn read_sources(root: &Path, facts: &mut ProjectFacts, on_progress: &(dyn Fn(Progress) + Sync))
    -> Result<(), ScanError>
{
    java_parser()?;                       // 1. gramer yüklenebiliyor mu? (bir kez, erken)
    let jobs = jobs(root, facts);         // 2. iş listesi: (modül, dosya, test mi)
    let outcomes: Vec<Outcome> = jobs     // 3. paralel ayrıştır
        .par_iter()
        .map_init(|| java_parser().expect("…"), |parser, job| {
            let outcome = parse_one(parser, root, job);
            let finished = done.fetch_add(1, Ordering::Relaxed) + 1;
            on_progress(Progress { done: finished, total });
            outcome
        })
        .collect();
    // 4. sonuçları modüllere dağıt (seri)
}
```

Dikkat edilecekler:

- **`&(dyn Fn(Progress) + Sync)`** — ilerleme geri çağrısı işçi iş parçacıklarından çağrılacağı için `Sync` olmak zorunda. Derleyici bunu bizim yerimize denetliyor; `Sync` olmayan bir kapanış (ör. içinde `Rc` taşıyan) vermeye çalışırsan derlenmez. Faz 4'te buraya Tauri'nin `Channel`'ı bağlanacak.
- **`AtomicUsize` ve `Ordering::Relaxed`** — sayaç sadece "kaç dosya bitti" bilgisini taşıyor, başka bir veriyle senkronize olması gerekmiyor. En gevşek sıralama yeterli.
- **Sonuçları dağıtmak seri.** `facts.modules[i].files.push(...)` paralel yapılsaydı `Mutex` gerekirdi. Bunun yerine paralel kısım sadece **değer üretiyor** (`Outcome`), paylaşılan duruma dokunmuyor. Rust'ta paralelliği kolaylaştırmanın en iyi yolu paylaşılan değiştirilebilir durumdan kaçınmak.

İş listesi kurulurken modülün kaynak klasörleri belirlenir:

| Durum | Kaynak klasörleri |
|---|---|
| Maven/Gradle, hiçbir şey beyan edilmemiş | `src/main/java`, `src/test/java` |
| Maven `<sourceDirectory>` / `<testSourceDirectory>` | Beyan edilen (parent'tan kalıtılır, her modülün kendi klasörüne göre çözülür) |
| Plain | Kök klasörün tamamı; yolunda `test`/`tests` parçası olanlar test sayılır |

### 3.7 Gerçek dünya bize ne öğretti?

Örnek projeler yeşilken iki gerçek projeyle denedik ve iki şey çıktı:

**1. Guava `src/` kullanıyor.** İlk denemede Guava'dan **0 dosya** okundu. Kök POM'da:

```xml
<build>
  <sourceDirectory>src</sourceDirectory>
  <testSourceDirectory>test</testSourceDirectory>
</build>
```

Bu ayar dört alt modüle kalıtılıyor. Plan'da "standart olmayan kaynak kökleri bilinen sınır" demiştik, ama Guava gibi yaygın bir projede çalışmayan bir araç kullanışsız. Çözüm Faz 1'in altyapısına küçük bir ek oldu: `RawPom`'a iki alan, `Effective`'e kalıtım, `ModuleFacts`'a `source_roots`. `${project.basedir}/src` ile `src` aynı klasör sayılıyor. Yeni bir test (`inherited_source_directories_are_honoured`) geçici bir klasörde aynı yapıyı kuruyor.

Gradle'ın `sourceSets { main { java { srcDirs = [...] } } }` bloğu ise hâlâ okunmuyor — bir sonraki "Kendin dene" görevi.

**2. tree-sitter-java her şeyi bilmiyor.** Guava'da 6 dosya `syntax errors` uyarısı verdi. Hepsinde aynı satır biçimi:

```java
public static int hashCode(@Nullable Object @Nullable ... objects)
```

Varargs `...` öncesindeki **tip annotation'ı** (`Object @Nullable ...`) geçerli Java ama `tree-sitter-java` 0.23 grameri bunu tanımıyor. tree-sitter'ın hataya dayanıklılığı sayesinde dosyanın geri kalanı (paket, import'lar, tipler, diğer referanslar) eksiksiz okunuyor; uyarı sadece "bu dosyanın olguları eksik olabilir" diyor. Commons Lang'da ise tek uyarı, içinde ikili (binary) fuzz verisi olan bir test dosyasından geliyor.

## 4. Takıldığın yerler

| Belirti | Neden | Çözüm |
|---|---|---|
| `field captures of struct QueryMatch is private` | tree-sitter 0.27'de alan yerine metot | `m.captures()` |
| `matches.next()` derlenmiyor | `QueryMatches` artık `Iterator` değil, `StreamingIterator` | `use streaming_iterator::StreamingIterator;` |
| `java`, `util` da tip olarak sayılıyor | `scoped_type_identifier`'ın içine inilmiş | O düğümde `descend = false` |
| `T`, `R`, `var` tip sanılıyor | Ağaçta hepsi `type_identifier` | Generic parametreleri topla, `var`'ı ele |
| `LoggerFactory`, `Money` (statik çağrılar) eksik | Ağaçta `identifier`, `type_identifier` değil | `method_invocation.object` / `field_access.object` büyük harfle başlıyorsa aday |
| Enum içindeki iç içe tip bulunmuyor | Üyeler `enum_body_declarations` içinde | O düğüme de in |
| Guava'dan 0 dosya | `<sourceDirectory>src</sourceDirectory>` | Maven kaynak klasörü desteği (3.7) |
| `Object @Nullable ... args` satırında sözdizimi hatası | Gramer sınırı | Uyarı olarak raporla; geri kalan olgular sağlam |
| Snapshot'lar makineden makineye değişiyor | Dizin gezme sırası dosya sistemine bağlı; `HashMap` sırası rastgele | Dosya listesini `sort()`, referanslar için `BTreeMap`, `par_iter().collect()` sırayı korur |
| Bir düğümün yapısını tahmin edemiyorum | — | Aşağıdaki döküm aracı |

**Ağaç döküm aracı.** Herhangi bir Java dosyasının ağacını alan adlarıyla yazdırır. Ayrı bir küçük crate olarak (`cargo new tsdump`) kurup `tree-sitter` ve `tree-sitter-java` ekle:

```rust
fn main() {
    let path = std::env::args().nth(1).unwrap();
    let src = std::fs::read_to_string(path).unwrap();
    let mut p = tree_sitter::Parser::new();
    p.set_language(&tree_sitter_java::LANGUAGE.into()).unwrap();
    let tree = p.parse(&src, None).unwrap();

    fn walk(n: tree_sitter::Node, src: &str, depth: usize, field: Option<&str>) {
        let leaf = if n.child_count() == 0 { format!(" {:?}", &src[n.byte_range()]) } else { String::new() };
        if n.is_named() {
            let field = field.map(|f| format!("{f}: ")).unwrap_or_default();
            println!("{}{field}{}{leaf}", "  ".repeat(depth), n.kind());
        }
        let mut c = n.walk();
        for (i, child) in n.children(&mut c).enumerate() {
            walk(child, src, depth + 1, n.field_name_for_child(i as u32));
        }
    }
    walk(tree.root_node(), &src, 0, None);
}
```

Bu bölümdeki tüm ağaç parçaları bu araçla alındı.

## 5. Ne öğrendik

- tree-sitter'da **isimli/isimsiz düğüm** ve **alan adı** kavramları; `children`, `named_children`, `child_by_field_name`, `children_by_field_name`.
- Yapısal gezinme ile tam dolaşmanın ne zaman hangisinin uygun olduğunu.
- `TreeCursor` ile **özyinelemesiz ön-sıralı dolaşma** ve etiketli döngüler.
- tree-sitter **sorgu dili** (`(import_declaration [(identifier) (scoped_identifier)] @path)`): kısa ama etrafında yine Rust kodu gerekiyor. Testte aynı sonucu verdiğini gösterdik; projede cursor yaklaşımında kaldık çünkü adım adım hata ayıklaması daha kolay.
- `rayon` ile veri paralelliği; `map_init` ile iş parçacığı başına kaynak; sırayı koruyan `collect`.
- `Send`/`Sync` sınırlarının derleyici tarafından denetlenmesi; `AtomicUsize` ile kilitsiz sayaç.
- Paylaşılan değiştirilebilir durumdan kaçınarak paralel kodu basit tutmak.
- Gerçek projelerle denemenin değeri: örnekler yeşilken Guava'nın kaynak klasörü ve gramerin varargs annotation sınırı ancak böyle ortaya çıktı.

## 6. Kendin dene

1. **Gradle `sourceSets`:** `atlas-shop-gradle`'daki `shop-core`'un kaynaklarını `src/java` klasörüne taşı ve `build.gradle.kts`'e `sourceSets { main { java { setSrcDirs(listOf("src/java")) } } }` ekle. Faz 1'in lexer'ını kullanarak bu kalıbı tanı ve `source_roots`'u doldur.
2. **Satır yerine "kod satırı":** `TypeFacts::lines` boş satırları ve yorumları da sayıyor. tree-sitter'da yorumlar `line_comment` / `block_comment` düğümleridir. Bir tipin yorum ve boş satır hariç satır sayısını hesaplayan bir fonksiyon yaz; hangisi bina boyutu için daha anlamlı?
3. **İlerlemeyi gör:** `inspect` örneğine `--progress` bayrağı ekle ve ilerlemeyi `\r312/1556` biçiminde aynı satırı güncelleyerek yazdır. Geri çağrı birden fazla iş parçacığından geldiği için çıktının karışmaması için ne yapman gerekir? (İpucu: `std::io::stderr().lock()`.)
