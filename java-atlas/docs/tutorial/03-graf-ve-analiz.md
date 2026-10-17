# Faz 3 — Bağımlılık Grafı ve Analiz

> **Önceki:** [02 — tree-sitter ile Java](02-tree-sitter-ile-java.md) · **Sonraki:** [04 — Tauri köprüsü](04-tauri-kopru.md)

## 1. Bu bölümde ne yapacağız

İki fazdır **olgu** topluyoruz: hangi modül neyi beyan ediyor, hangi dosya hangi adı anıyor. Bu fazda olguları **anlama** çeviriyoruz:

- Her adı Java'nın kapsam kurallarına göre çözüp **tip → tip** kenarları kurmak
- Dış paketleri JDK'ya, beyan edilmiş bir kütüphaneye ya da "bilinmeyen adaya" bağlamak
- Kenarları **tip → paket → modül** seviyelerine toplamak (haritanın üç ölçeği)
- **Döngüleri** bulmak (Tarjan)
- **JPMS kaçak geçişlerini** bulmak
- Build dosyası ile kodu karşılaştırmak: **kullanılmayan** ve **beyan edilmemiş** bağımlılıklar
- Paket ve modül başına **Ca / Ce / I** metrikleri

Bölümün sonunda:

```bash
cargo run -p java-atlas-scan --example inspect -- samples/atlas-bank --atlas
```

```
atlas-bank · 5 module(s) · 8 package(s) · 18 type(s) · 4 librar(y/ies)
…
cycles:
  ⟲ com.atlasbank.persistence ⇄ com.atlasbank.persistence.internal  — weakest: com.atlasbank.persistence → com.atlasbank.persistence.internal (2)
  ⟲ com.atlasbank.service.account ⇄ com.atlasbank.service.audit  — weakest: com.atlasbank.service.audit → com.atlasbank.service.account (2)

jpms violations:
  ✗ com.atlasbank:bank-api → com.atlasbank.persistence.internal: package not exported  (…/AccountController.java:3, …/AccountController.java:16)

build file vs code:
  ✕ declared, unused: com.atlasbank:bank-common → org.apache.commons:commons-lang3
  ! used, not declared (arrives transitively): com.atlasbank:bank-service → com.atlasbank:bank-common
  ! used, not declared (arrives transitively): com.atlasbank:bank-api → com.atlasbank:bank-persistence

metrics (main code, JDK excluded):
  com.atlasbank:bank-common  Ca 2 · Ce 0 · I 0.00
  …
    com.atlasbank.api  Ca 0 · Ce 5 · I 1.00
17 file(s): scan 9ms, analysis 1ms
```

Örnek projeye bilerek koyduğumuz her şey bulundu: döngü, kaçak geçiş, kullanılmayan `commons-lang3`, beyan edilmemiş `bank-persistence`. Üstüne **planlamadığımız** iki şey de çıktı (bkz. 3.10). Analiz, Guava'nın 1.556 dosyasında ~50 ms sürüyor.

## 2. Neden böyle

### Arena graf

Rust'ta graf kurmanın "nesne yönelimli" yolu `Rc<RefCell<Node>>`'dur: her düğüm komşularına sayaçlı, iç değiştirilebilir referans tutar. Sonuç: her erişimde `borrow()`/`borrow_mut()`, çalışma zamanında panik riski, döngüsel `Rc`'lerle bellek sızıntısı.

**Arena** yaklaşımı: tüm düğümler tek bir `Vec<Node>`'da, birbirlerine **indeksle** atıfta bulunurlar:

```rust
pub struct NodeId(pub u32);

pub struct Node {
    pub id: NodeId,
    pub kind: NodeKind,
    pub name: String,
    pub fqn: String,
    pub parent: Option<NodeId>,   // tip → paket → modül
    pub is_test: bool,
}

pub struct Edge {
    pub from: NodeId,
    pub to: NodeId,
    pub weight: Weight,           // { main, test }
    pub evidence: Vec<Evidence>,  // { file, line }, en fazla 20
}
```

`NodeId` `Copy`'dir; kenarlar sadece iki sayı taşır. Ödünç alma denetleyicisinin itiraz edeceği hiçbir şey kalmaz. Bedeli: yanlış bir indeks panik yaratır — ama düğümleri sadece biz ekliyoruz ve hiç silmiyoruz, yani her `NodeId` geçerli.

easy-git'teki commit grafında da aynı yöntemi kullanmıştık. Burada farkı: düğümlerin **türleri** var.

```rust
pub enum NodeKind {
    Module { id: String, jpms: Option<JpmsFacts> },   // bölge
    Package { exported: Option<bool> },               // şehir (None = JPMS yok)
    Type { kind: TypeKind, lines: u32, file: String },// bina
    Library { group, artifact, version, packages },   // ada
    Jdk,                                              // anakara
    Unknown,                                          // bilinmeyen ada
}
```

### Neden iki geçiş?

`AccountService.java` dosyasında `AuditLog` adı geçiyor. Bunun bir proje tipi mi yoksa bir dış kütüphane mi olduğunu bilmek için **tüm** proje tiplerini önceden tanımamız gerek. Bu yüzden:

1. **Bildirim geçişi:** modül, paket, tip düğümlerini oluştur; sembol tablosunu (`tam ad → NodeId`) doldur. Beyan edilmiş her kütüphane için bir ada oluştur.
2. **Çözümleme geçişi:** her dosyanın import'larını ve ad adaylarını sembol tablosunda ara, kenarları kur.

Derleyiciler de aynı nedenle çok geçişlidir.

### Dosya = tip birimi

Faz 2'de referansları **dosya** başına topladık, tip başına değil. Bir dosyanın tüm kullanımlarını o dosyadaki **ilk üst seviye tipe** bağlıyoruz. İç içe tipler (`Account.Status`) dış tiplerinin dosyasını paylaştığı için kayıp küçük; tek dosyada birden fazla üst seviye tip ise Java'da nadir (ve en fazla bir tanesi `public` olabilir).

### Ağırlık: main ve test ayrı

Aynı paket hem ana hem test koduna ev sahipliği yapabilir (`com.atlasbank.service.account` içinde `AccountSummaryTest` de var). Kenar ağırlığını iki sayı olarak tutuyoruz:

```rust
pub struct Weight { pub main: u32, pub test: u32 }
```

Döngüler ve metrikler **sadece ana kodla** hesaplanıyor: bir testin iki paketi birden kullanması mimari bir sorun değil. Haritada test filtresi (Faz 7) aynı ayrımı kullanacak.

## 3. Adım adım uygulama

### 3.1 Dosya düzeni

```
crates/java-atlas-core/src/
├── lib.rs
├── facts.rs          ← LibraryIndex eklendi
├── intern.rs         ← yeni: string interning
└── atlas/
    ├── mod.rs        ← tipler, Atlas::build, sorgu yardımcıları, birim testleri
    ├── resolve.rs    ← geçiş 1 + 2, dış eşleme
    ├── analysis.rs   ← toplama, Tarjan, JPMS, bulgular, metrikler
    └── describe.rs   ← düz metin rapor
crates/java-atlas-scan/src/
└── jars.rs           ← yeni: yerel önbellekteki jar'ların paket listesi
crates/java-atlas-scan/tests/
└── atlas.rs          ← yeni: uçtan uca testler + snapshot
```

`java-atlas-core`'un `[dependencies]` bölümü **hâlâ boş**. Graf, Tarjan, interning — hepsi standart kütüphaneyle.

### 3.2 String interning — `intern.rs`

Sembol tablosu sürekli `"com.atlasbank.service.account.AccountService"` gibi uzun dizgileri hash'leyip karşılaştırıyor. Interning her farklı dizgiyi bir kez saklar ve bir `u32` ile anar:

```rust
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, PartialOrd, Ord)]
pub struct Sym(u32);

pub struct Interner {
    ids: HashMap<Box<str>, Sym>,
    strings: Vec<Box<str>>,
}

impl Interner {
    pub fn intern(&mut self, s: &str) -> Sym { /* yoksa ekle */ }
    pub fn get(&self, s: &str) -> Option<Sym> { /* ekleme yapmaz */ }
    pub fn resolve(&self, sym: Sym) -> &str { /* geri çevir */ }
}
```

İki ayrı arama metodu bilinçli:

- `intern` bildirim geçişinde: yeni adları ekler.
- `get` çözümleme geçişinde: **eklemez**. `None` dönmesi zaten bir cevaptır — "projede bu adla bir tip hiç yok". Çözümleme sırasında tabloyu büyütmek, var olmayan tipler için anlamsız girdiler üretirdi.

`Box<str>` yerine `String` de olurdu; `Box<str>` kapasite alanı taşımaz, değişmeyecek dizgiler için bir kelime daha küçük.

### 3.3 Java'nın kapsam kuralları — `resolve_name`

Bir dosyada geçen basit bir ad, Java Dil Belirtimi'ndeki (JLS §6.4.1) sırayla çözülür:

| Sıra | Kaynak | Örnek |
|---|---|---|
| 1 | Tek tip import'u | `import b.Money;` → `Money` = `b.Money` |
| 2 | Aynı paket | `a` paketindeki `User`, `Money` dediğinde `a.Money` |
| 3 | Joker import'lar | `import c.*;` → `Helper` = `c.Helper` (varsa) |
| 4 | Static joker import'un iç tipleri | `import static a.Outer.*;` → `Inner` = `a.Outer.Inner` |
| 5 | `java.lang` | `String`, `Override`, `IllegalStateException` … |
| 6 | Tam nitelikli ad | `java.util.concurrent.atomic.AtomicLong` |
| — | Hiçbiri | `LOG`, kaçırılmış bir generic parametre → **düşer** |

```rust
fn resolve_name(&self, name: &str, package: &str, single: &HashMap<&str, String>,
                on_demand: &[&str], static_owners: &[String]) -> Option<Target> {
    let (first, rest) = match name.split_once('.') { /* "Account.Status" → ("Account", Some("Status")) */ };

    if let Some(fqn) = single.get(first) {
        return self.resolve_fqn(&with_rest(fqn));
    }
    if let Some(id) = self.internal_type(&with_rest(&qualify(package, first))) {
        return Some(Target::Internal(id));
    }
    for p in on_demand { /* … */ }
    for owner in static_owners { /* … */ }
    if JAVA_LANG.contains(&first) {
        return Some(Target::External("java.lang".to_owned()));
    }
    if rest.is_some() && first.starts_with(char::is_lowercase) {
        return self.resolve_fqn(name);
    }
    None
}
```

Sıra önemli: **tek tip import'u aynı paketi gölgeler** (shadowing), aynı paket joker import'u gölgeler. `javas_scoping_rules_in_order` testi tam olarak bunu sınıyor: `a` ve `b` paketlerinde birer `Money` var; `import b.Money` yazan dosya `b.Money`'ye, `a` paketindeki dosya `a.Money`'ye bağlanmalı.

İç içe adlarda (`Account.Status`) ilk parça çözülür, geri kalanı eklenir. `import m.Account.Status;` gibi doğrudan iç tip import'ları da Faz 2'de iç tipleri `Outer.Inner` adıyla kaydettiğimiz için sembol tablosunda doğrudan bulunur.

**Import'lar da kullanımdır.** Kullanılmayan bir import bile derleme zamanında o sınıfı gerektirir. Her tek tip import'u ağırlığa 1 ekler; gövdedeki kullanımlar üstüne eklenir. Joker import tek başına hiçbir şey adlandırmaz, kenar üretmez; sadece adların çözümünde kullanılır.

Faz 2'de `LOG`'u "aday" olarak topladığımızı söylemiştik. İşte düştüğü yer: tablolarda yok, `java.lang` değil, küçük harfle başlayan bir paket öneki de değil → `None`.

### 3.4 Dış paketler: JDK mı, ada mı, bilinmeyen mi?

Proje dışı bir tam ad (`org.slf4j.Logger`) önce paketine indirgenir — ilk büyük harfli parçadan öncesi:

```rust
pub(super) fn package_of(fqn: &str) -> String  // "java.util.Map.Entry" → "java.util"
```

Sonra üç olasılık:

1. **JDK:** `java.*`, `jdk.*`, `sun.*` ve JDK ile gelen `javax` paketleri (`javax.sql`, `javax.swing` …). Dikkat: `javax.inject`, `javax.servlet` gibileri **kütüphanedir**, liste bu yüzden açık yazıldı.
2. **Beyan edilmiş bir kütüphane** (bkz. 3.5)
3. **Bilinmeyen:** hiçbir kütüphane açıklamıyor → `NodeKind::Unknown`. Haritada "bilinmeyen ada". Genellikle transitive bir bağımlılıktan gelen paket.

Eşleme pahalı olduğu için `(modül, paket)` başına önbelleğe alınıyor.

### 3.5 Paket → kütüphane: önce kanıt, sonra tahmin

**Kanıt: jar'ın içi.** `org.slf4j.Logger`'ın `slf4j-api`'den geldiğini kesin olarak bilmenin tek yolu jar'a bakmak. Hiçbir şey indirmiyoruz; geliştirici projeyi bir kez derlediyse jar'lar zaten diskte:

| Önbellek | Yol |
|---|---|
| Maven | `~/.m2/repository/org/slf4j/slf4j-api/2.0.16/slf4j-api-2.0.16.jar` |
| Gradle | `~/.gradle/caches/modules-2/files-2.1/org.slf4j/slf4j-api/2.0.16/<hash>/slf4j-api-2.0.16.jar` |

Bir jar bir zip dosyasıdır ve zip'in sonunda bir **içindekiler tablosu** (central directory) vardır. Sadece onu okuyoruz — hiçbir sınıfı açmadan:

```rust
pub fn packages_in_jar(jar: &Path) -> Result<Vec<String>, String> {
    let archive = zip::ZipArchive::new(File::open(jar).map_err(|e| e.to_string())?)
        .map_err(|e| e.to_string())?;
    let packages: BTreeSet<String> = archive
        .file_names()
        .filter_map(Result::ok)                     // zip 9: çözülemeyen adlar Err
        .filter(|n| n.ends_with(".class") && !n.starts_with("META-INF/"))
        .filter_map(|n| n.rsplit_once('/').map(|(dir, _)| dir.replace('/', ".")))
        .collect();
    Ok(packages.into_iter().collect())
}
```

`zip` crate'ini `default-features = false` ile ekledik: sıkıştırma algoritmalarının hiçbirine ihtiyacımız yok, derleme süresi ve bağımlılık ağacı küçülüyor.

Hangi önbelleklere bakılacağı `ScanOptions` ile veriliyor:

```rust
pub struct ScanOptions { pub library_caches: Vec<LibraryCache> }

ScanOptions::with_default_caches()   // ~/.m2 ve Gradle önbelleği (varsa)
ScanOptions::default()               // hiçbirine bakma
```

**Testler `default()` kullanıyor.** Aksi hâlde snapshot'lar makinenin `~/.m2` içeriğine göre değişirdi. Jar yolunu sınayan test (`a_cached_jar_turns_a_guess_into_a_fact`) geçici bir klasörde kendi küçük jar'ını üretiyor.

**Tahmin: ad benzerliği.** Jar bulunamazsa paket adı ile kütüphanenin koordinatlarını karşılaştırıyoruz. İlk sürüm sadece "groupId ile ortak önek" bakıyordu; Guava ile denemede yetersiz kaldı. Şimdiki puanlama:

| Sinyal | Örnek | Puan |
|---|---|---|
| Paket groupId'nin tamamının altında | `org.slf4j` ⊂ `org.slf4j` | 100 + 10/parça |
| Paket groupId'nin **üstünde** | `org.junit` vs `org.junit.jupiter` | 0 |
| 3+ ortak parça | `com.fasterxml.jackson.core` ~ `…jackson.databind` | 50 + 10/parça |
| Sadece 2 ortak parça | `com.google.guava` ~ `com.google.common` | 20 (zayıf) |
| artifactId'nin tamamı bir paket parçası | `truth` ∈ `com.google.common.truth` | +40 |
| artifactId'nin bir kelimesi bir paket parçası | `commons-text` → `text` | +30 |

Karar kuralları:

- Önce modülün **kendi** beyan ettiği kütüphaneler arasında aranır, sonra projenin tümünde (ikincisinde sadece güçlü eşleşmeler, ≥ 40).
- Birinciliği **farklı groupId'lerden** iki kütüphane paylaşıyorsa: tahmin yok (`com.google.guava:failureaccess` ile `com.google.j2objc` gibi).
- Aynı groupId'den paylaşıyorlarsa (`gwt-dev` / `gwt-user`): ilki seçilir — haritada zaten aynı takımadadırlar.

`name_scores_rank_the_right_library_first` testi zor vakaları tek tek sınıyor: JUnit 4 ile 5 yan yana, `commons-lang3` ile `commons-text` yan yana, Truth ile Guava yan yana.

Ayrıntı panelinde (Faz 7) her adanın paketleri yanında yöntemi de gösterilecek: `[jar]` kesin, `[name]` tahmin.

### 3.6 Toplama: tip → paket → modül

```rust
fn lift(nodes: &[Node], mut id: NodeId, steps: usize) -> NodeId {
    for _ in 0..steps {
        match nodes[id.index()].parent {
            Some(parent) => id = parent,
            None => break,
        }
    }
    id
}

fn aggregate(nodes: &[Node], type_edges: &[Edge], steps: usize) -> Vec<Edge> {
    let mut merged: BTreeMap<(NodeId, NodeId), Edge> = BTreeMap::new();
    for e in type_edges {
        let from = lift(nodes, e.from, steps);
        let to = if nodes[e.to.index()].is_internal() { lift(nodes, e.to, steps) } else { e.to };
        if from == to {
            continue;   // aynı şehrin içi: şehirler arası yol değil
        }
        merged.entry((from, to)).or_insert_with(|| /* boş kenar */).merge(e.weight, &e.evidence);
    }
    merged.into_values().collect()
}
```

Dış düğümlerin (ada, anakara) ebeveyni yok; her seviyede aynı kalırlar. `BTreeMap` yine sıralı ve kararlı çıktı veriyor.

`edges_add_up_from_types_to_modules` testi bir **değişmezi** (invariant) sınıyor: yukarı çıktıkça toplam ağırlık ancak azalabilir (aynı paket/modül içinde kalan kullanımlar düşer), asla artamaz.

### 3.7 Döngüler: Tarjan algoritması

Bir **güçlü bağlı bileşen** (SCC), içindeki her düğümden diğer her düğüme gidilebilen en büyük kümedir. Boyutu 1'den büyük her SCC bir döngüdür. Tarjan bunu tek bir derinlik öncelikli aramada bulur:

- Her düğüme ziyaret sırasına göre bir `index` verilir.
- `low`: o düğümün alt ağacından, **yığında hâlâ duran** düğümlere giderek ulaşılabilen en küçük `index`.
- Bir düğümün `low == index` ise o bir bileşenin köküdür: yığında onun üstündeki her şey o bileşene aittir.

```rust
fn visit(s: &mut State, v: usize) {
    s.index[v] = Some(s.next);
    s.low[v] = s.next;
    s.next += 1;
    s.stack.push(v);
    s.on_stack[v] = true;

    for &w in &s.adjacency[v] {
        match s.index[w] {
            None => {
                visit(s, w);
                s.low[v] = s.low[v].min(s.low[w]);
            }
            Some(w_index) if s.on_stack[w] => s.low[v] = s.low[v].min(w_index),
            Some(_) => {}   // başka, bitmiş bir bileşende
        }
    }

    if Some(s.low[v]) == s.index[v] {
        let mut component = Vec::new();
        while let Some(w) = s.stack.pop() {
            s.on_stack[w] = false;
            component.push(w);
            if w == v { break; }
        }
        s.components.push(component);
    }
}
```

İç içe tanımlanmış `fn visit` ve tüm durumu taşıyan bir `State` yapısı: kapanışlar (closure) kendilerini özyinelemeli çağıramadığı için Rust'ta bu kalıp yaygın. Algoritma `NodeId` değil `0..n` üzerinde çalışıyor; paketleri yoğun indekslere çeviren küçük bir `HashMap` ile.

Özyineleme derinliği en fazla paket sayısı kadar — yüzlerle sınırlı, sorun değil. (Faz 2'de ağaç dolaşmayı özyinelemesiz yapmıştık; orada derinlik **ifade** iç içeliğine bağlıydı ve sınırsızdı.)

Her döngü için **en zayıf kenar** (en az kullanımlı) raporlanıyor: döngüyü kırmanın en ucuz yolu muhtemelen orası.

### 3.8 JPMS: kapısız sınır geçişi

Her tip → tip kenarı farklı modüller arasındaysa ve hedef modülde `module-info.java` varsa iki soru sorulur:

1. **Paket export ediliyor mu?** `exports p;` ya da `exports p to <kullanan modül>;`. Değilse → `NotExported`.
2. Kullanan modülde de `module-info.java` varsa: **hedef modülü okuyabiliyor mu?** Doğrudan `requires` ya da `requires transitive` zinciri ("örtük okunabilirlik"):

```rust
fn reads(nodes: &[Node], from: &JpmsFacts, to: &str) -> bool {
    // BFS: ilk adımda tüm requires; sonraki adımlarda sadece requires transitive
}
```

Değilse → `NotRead`.

`bank-api`'nin `module-info.java`'sı yok, yani sınıf yolunda (classpath) çalışıyor ve JVM onu durdurmaz. Ama `bank-persistence` `persistence.internal`'ı **bilerek** dışarı açmamış. Bu bir niyet ihlali ve harita onu kırmızı kesikli yol olarak gösterecek.

Guava'da da iki ihlal çıktı: `guava-tests`, `guava` modülünün export etmediği `com.google.common.base.internal` ve `com.google.thirdparty.publicsuffix` paketlerine test kodundan erişiyor. Test kodunun iç paketlere erişmesi bilinçli bir tercih — ihlaller bu yüzden kanıtlarıyla birlikte gösteriliyor, yargı kullanıcıda.

### 3.9 Build dosyası ile kod

Dört bulgu türü:

| Bulgu | Nasıl |
|---|---|
| Kullanılmayan modül bağımlılığı | Beyan var, modül seviyesinde kenar yok |
| Kullanılmayan kütüphane | Beyan var (kalıtılmış ve `runtime` hariç), kenar yok |
| Beyan edilmemiş modül bağımlılığı | Kenar var, beyan yok. **Dolaylı ulaşılabilir mi?** |
| Beyan edilmemiş kütüphane | Kenar var, modülün beyanlarında yok |

"Dolaylı ulaşılabilir" Maven ile Gradle'da farklı:

```rust
let transitive = |scope: Scope, exposed: bool| match facts.build_system {
    BuildSystem::Maven => matches!(scope, Scope::Compile | Scope::Runtime),
    BuildSystem::Gradle => exposed,   // sadece api(...)
    BuildSystem::Plain => false,
};
```

Maven'da `compile` kapsamlı her bağımlılık tüketicilere geçer; Gradle'da sadece `api` ile beyan edilenler. `bank-api → bank-persistence` derleniyor çünkü `bank-service` onu `compile` ile istiyor — ama `bank-service` bir gün bu bağımlılığı bırakırsa `bank-api` kırılır. Bulgu bunu söylüyor.

**Neden kalıtılmış ve `runtime` bağımlılıklar "kullanılmayan" sayılmıyor?** Kök POM'daki JUnit beş modüle kalıtılıyor, üçünde test yok. Beşini birden "kullanılmayan" diye işaretlemek gürültü olurdu; karar parent'ın. `runtime` bağımlılıklar (ör. `slf4j-simple`) ise tanımı gereği kodda görünmez.

### 3.10 Planlamadığımız iki bulgu

Analiz `atlas-bank`'te bizim koymadığımız iki şey buldu ve ikisi de doğru:

**1. `persistence` ⇄ `persistence.internal` döngüsü.**

```java
// com.atlasbank.persistence.Repositories
public static AccountRepository inMemory() { return new InMemoryAccountStore(); }

// com.atlasbank.persistence.internal.InMemoryAccountStore
public class InMemoryAccountStore implements AccountRepository { … }
```

API paketindeki fabrika, iç paketteki gerçeklemeyi oluşturuyor; gerçekleme de API paketindeki arayüzü uyguluyor. Çok yaygın bir kalıp — ve paket seviyesinde gerçek bir döngü. Bunu bir hata olarak düzeltmedik; tam tersine örnekte bırakıyoruz, çünkü atlasın gerçek projelerde göstereceği en tipik döngü bu.

**2. `bank-service → bank-common` beyan edilmemiş.** `TransferService` doğrudan `Money`'yi import ediyor ama `bank-service`'in POM'unda `bank-common` yok; `bank-domain` üzerinden geliyor. Testler bu iki bulguyu da sabitliyor.

### 3.11 Metrikler

```rust
pub struct Metrics { pub ca: u32, pub ce: u32 }

impl Metrics {
    pub fn instability(self) -> Option<f64> {
        let total = self.ca + self.ce;
        (total > 0).then(|| f64::from(self.ce) / f64::from(total))
    }
}
```

- **Ca** (afferent): bu pakete bağımlı **paket** sayısı
- **Ce** (efferent): bu paketin bağımlı olduğu düğüm sayısı — proje paketleri, adalar, bilinmeyenler; **JDK hariç** (JDepend'in varsayılan filtresi gibi: JDK herkesin bağımlılığı, bilgi taşımıyor)
- **I = Ce / (Ca + Ce)**: 0 sağlam (çok kullanılan, az bağımlı), 1 kararsız (kimse kullanmıyor, çok bağımlı)

`atlas-bank`'te `com.atlasbank.common` 0.00, `com.atlasbank.api` 1.00 — beklenen tablo: altyapı sağlam, giriş noktası kararsız. "Kararlı bağımlılıklar ilkesi" (Stable Dependencies Principle): bağımlılıklar I'nın azaldığı yöne akmalı. Faz 10'da soyutluk (A) ile birlikte A–I grafiği çizilecek.

### 3.12 Gerçek projelerde

| Proje | Dosya | Tarama | Analiz | Not |
|---|---|---|---|---|
| atlas-bank | 17 | 9 ms | 1 ms | |
| Commons Lang | 628 | ~0,5 sn | ~12 ms | 13 paketlik dev bir döngü (gerçek) |
| Guava | 1.556 | ~0,9 sn | ~50 ms | 2 JPMS ihlali (test kodu), 3 bilinmeyen paket |

*(2 çekirdekli bulut makinesi, release derleme, önbellek yok.)*

Guava'da bilinmeyen kalan üç paket öğretici:

- `com.google.common.util.concurrent.internal` → `failureaccess` jar'ında. Adı hiçbir şeye benzemiyor; ancak jar indeksi çözebilir.
- `javax.annotation` → `jsr305` jar'ında. Aynı durum.
- `com.google.gwt.core.client` → aslında `gwt-user`'da; ad benzerliği `gwt-dev`'i buldu. Aynı takımada, ama yanlış ada.

Üçü de senin makinende, `~/.m2` doluysa `[jar]` olarak doğru çözülür.

## 4. Takıldığın yerler

| Belirti | Neden | Çözüm |
|---|---|---|
| `file_names()` üzerinde `ends_with` bulunamıyor | zip 9'da `file_names()` `Result<Cow<str>, _>` döner | `.filter_map(Result::ok)` |
| Snapshot'lar makineden makineye farklı | `~/.m2` taranıyor | Testlerde `ScanOptions::default()` |
| `org.junit` JUnit 5'e bağlanıyor | `org.junit.jupiter` ile 2 ortak parça | Paket groupId'nin **üstündeyse** önek puanı 0 |
| `failureaccess`, `j2objc` gibi alakasız kütüphaneler eşleşiyor | Sadece `com.google` ortak | Farklı groupId'ler arasında eşitlikte tahmin yok |
| "Bilinmeyen" listesinde `s ~ t ...` gibi çöp | İkili veri içeren bir test dosyası (Commons Lang fuzz testi) | `is_qualified_name` ile Java adı olmayan adayları ele |
| Joker import'tan gelen tip bulunmuyor | `ImportFacts.path`'e `.*` dahil edilmiş | Faz 2'deki sözleşme: yol `.*` içermez, `on_demand` bayrağı ayrı |
| Kapanış kendini çağıramıyor (Tarjan) | Rust kapanışları özyinelemeli değil | İç `fn` + durum yapısı |
| Döngü önerisi her çalıştırmada farklı | Eşit ağırlıklı kenarlar | Eşitlikte paket adına göre sırala |

## 5. Ne öğrendik

- **Arena graf:** `Vec<Node>` + `NodeId(u32)`; `Rc<RefCell>`'siz, ödünç alma kavgasız graf.
- **String interning** ve `intern`/`get` ayrımının anlamı.
- **İki geçişli çözümleme** ve Java'nın ad gölgeleme kuralları.
- **Tarjan'ın SCC algoritması**; iç `fn` ile özyinelemeli durum taşıma.
- **Seviye toplama** ve değişmez (invariant) testleri.
- JPMS'in **export** ve **örtük okunabilirlik** kuralları; BFS ile `requires transitive` zinciri.
- Maven ile Gradle'ın **transitive** bağımlılık anlayışı farkı.
- **Kanıt ile tahmini ayırmak:** jar indeksi (`[jar]`) ile ad puanlaması (`[name]`); tahminin ne zaman cevap vermemesi gerektiği.
- **Zip central directory:** bir arşivi açmadan içindekiler tablosunu okumak.
- Örnek projenin bile bizi şaşırtabileceği: analiz, koymadığımız iki gerçek sorunu buldu.

## 6. Kendin dene

1. **Modül döngüleri:** Şu an döngüler sadece paket seviyesinde aranıyor. Build dosyalarında döngü olamaz, ama **kodda** olabilir (beyan edilmemiş bağımlılıklarla). `tarjan`'ı modül kenarlarına da uygula ve `describe`'a ekle.
2. **Yol ağırlığı mı, kenar sayısı mı?** `weakest` şu an en az **kullanımlı** kenarı seçiyor. Bir kenarı kaldırmanın gerçek maliyeti, kaç **farklı tipin** etkilendiğidir. `type_edges` üzerinden, bir paket kenarına katkı veren farklı kaynak tip sayısını hesaplayıp `weakest`'i buna göre seçen bir sürüm yaz. `atlas-bank`'te sonuç değişiyor mu?
3. **`javax` listesi:** `JDK_JAVAX` listesine bir paket eksik olduğunda ne olur? `javax.annotation.processing` satırını sil, `javax.annotation` kullanan bir test yaz (ör. `@Generated`) ve sonucun `Unknown`'a mı yoksa bir kütüphaneye mi gittiğine bak.
