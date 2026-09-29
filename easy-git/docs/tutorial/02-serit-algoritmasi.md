# Faz 2 — Şerit Algoritması

> **Önceki:** [01 — gix ile git okuma](01-gix-ile-git-okuma.md) · **Sonraki:** [03 — Tauri köprüsü](03-tauri-kopru.md)

## 1. Bu bölümde ne yapacağız

easy-git'in kalbini yazacağız: ham commit listesini alıp her commit'i bir **şeride (hat)** yerleştiren, hatlar arası **geçişleri** (fork, merge, cherry-pick) çıkaran algoritma. Çıktı `MetroMap` adlı tek bir yapı olacak; Faz 4'te ön yüz onu olduğu gibi çizecek.

Bölümün sonunda fixture repo şu metin çizimine dönüşüyor (snapshot testinden birebir):

```
main                         |oo--------@------@oo|
develop                      | <o---@----@--@o|
release/1.0                  |               <o|
hotfix/crash                 | <-------o|
feature/login                |  <ooo|
feature/payments             |      <oo|
feature/old-search (deleted) |           <oo|

fork        main @1 -> develop @2
fork        develop @2 -> feature/login @3
merge       feature/login @5 -> develop @6
...
cherry-pick feature/payments @8 -> main @18
```

`o` istasyon, `@` aktarma (merge) istasyonu, `<` hattın ayrıldığı makas. Metro haritası, daha ekrana çizilmeden terminalde görünür oldu.

## 2. Neden böyle

### Sorun: git "bu commit hangi dalda yapıldı" bilgisini saklamaz

Faz 1'de gördük: commit'te sadece parent'lar var. `feature/login` dalı merge edildikten sonra o üç commit hem `feature/login`'den hem `develop`'tan hem de `main`'den erişilebilir. Hangisine ait saymalıyız?

İnsan zihnindeki cevap şudur: *"Commit'i ilk yapıldığı dalda göster."* Bunu yaklaşık olarak bulmanın iki anahtarı var:

1. **First-parent zinciri.** Bir dalda `git commit` ya da `git merge` yaptığında, yeni commit'in *ilk* parent'ı her zaman o dalın önceki ucu olur. Merge ile gelenler 2., 3. parent'tır. Yani bir dalın ucundan başlayıp **sadece ilk parent'ları** takip edersek, o dalda "yerinde" yapılmış commit'lerin omurgasını buluruz.
2. **Öncelik.** Aynı commit birden fazla dalın first-parent zincirinde olabilir (örneğin `develop` `main`'den ayrılmadan önceki commit'ler). Kararı dalların önem sırası verir: `main` > `develop` > `release/*` > `hotfix/*` > `feature/*` > diğerleri.

### Algoritma (planın 5.3 bölümü)

```
1. Dalları önceliğe göre sırala.
2. Her dal için ucundan first-parent yürü; sahipsiz commit'leri al,
   sahipli bir commit'e çarpınca dur → orası dalın ayrıldığı nokta (fork).
3. Hâlâ sahipsiz commit kaldıysa bunlar silinmiş dallara aittir.
   En yeniden eskiye tara; ilk sahipsiz commit silinmiş bir dalın ucudur.
   Adını, onu yutan merge commit'in mesajından çıkar.
4. Her commit için: ilk parent başka şeritteyse → Fork,
   2.+ parent başka şeritteyse → Merge.
5. "(cherry picked from commit X)" satırı varsa ve X pencerede → CherryPick.
```

Adım 4, geçişleri yürüyüş sırasında değil **sonradan, tek bir kuraldan** türetiyor. Yürüyüş sırasında fork'u "keşfetmeye" çalışmak daha karmaşık ve hataya açık olurdu; ayrıştırılmış halde her adım tek başına test edilebiliyor.

### X ekseni: neden tarihe göre değil?

Makine saatleri yanlış olabilir, rebase tarihleri karıştırır. Tarihe göre sıralasak bir çocuk commit parent'ının soluna düşebilir ve bütün eğriler geriye doğru kıvrılır. Bunun yerine **topolojik sıralama** (Kahn algoritması) kullanıyoruz: bir commit ancak tüm parent'ları yerleştikten sonra yerleşir; aynı anda hazır olan commit'ler arasında en eski tarihli önce gelir. Böylece eksen saate *kabaca* uyar ama asla parent'tan önce çocuk gelmez.

### Graf Rust'ta nasıl tutulur?

C#'ta `class Node { List<Node> Parents; }` yazar geçerdik. Rust'ta `Rc<RefCell<Node>>` ile aynı şeyi yapmaya çalışmak ownership kavgasına dönüşür. Çözüm: **arena / indeks tabanlı** yapı.

```rust
pub struct MetroMap {
    pub commits: Vec<Commit>,     // topolojik sırada; indeks = sütun
    pub lane_of: Vec<LaneId>,     // commits ile paralel: her commit'in şeridi
    pub lanes: Vec<Lane>,
    pub transitions: Vec<Transition>,
    // ...
}
pub type Column = usize;
pub type LaneId = usize;
```

İlişkiler `usize`. Borrow checker'ın itiraz edeceği hiçbir referans yok; üstelik `Vec` bellekte bitişik, hızlı. Bu, Rust'ta graf/ağaç yazarken en çok başvurulan kalıp (ECS mimarisinin de temel fikri).

## 3. Adım adım uygulama

### 3.1 Öncelik listesi — `priority.rs`

```rust
pub struct BranchPriority { patterns: Vec<String> }

impl Default for BranchPriority {
    fn default() -> Self {
        Self::new(["main", "master", "trunk", "develop", "dev", "release/*", "hotfix/*", "feature/*"])
    }
}

impl BranchPriority {
    /// Lower is more important. Unmatched names rank after every pattern.
    pub fn rank(&self, branch: &str) -> usize {
        self.patterns
            .iter()
            .position(|pattern| match pattern.strip_suffix('*') {
                Some(prefix) => branch.starts_with(prefix),
                None => branch == pattern,
            })
            .unwrap_or(self.patterns.len())
    }
}
```

`new` metodunun imzası dikkat çekici: `I: IntoIterator<Item = S>, S: Into<String>`. Böylece hem `["main", "develop"]` (dizi, `&str`) hem de `Vec<String>` kabul ediliyor. Faz 9'da ayarlardan gelen listeyi de aynı fonksiyonla vereceğiz.

### 3.2 Silinmiş dalın adını bulmak — `infer.rs`

```rust
pub fn branch_from_merge_message(summary: &str) -> Option<String> {
    let s = summary.trim();
    // git:       Merge branch 'feature/x'  |  Merge branch 'feature/x' into develop
    // git pull:  Merge remote-tracking branch 'origin/feature/x'
    for prefix in ["Merge branch '", "Merge remote-tracking branch '"] {
        if let Some(rest) = s.strip_prefix(prefix) {
            let name = rest.split('\'').next()?;
            return non_empty(strip_remote(name));
        }
    }
    // GitHub:    Merge pull request #42 from user/feature/x
    if let Some(rest) = s.strip_prefix("Merge pull request #") {
        let (_, from) = rest.split_once(" from ")?;
        let (_owner, branch) = from.split_once('/')?;
        return non_empty(branch.split_whitespace().next()?);
    }
    // Bitbucket: Merged in feature/x (pull request #12)
    if let Some(rest) = s.strip_prefix("Merged in ") {
        return non_empty(rest.split_whitespace().next()?);
    }
    None
}
```

Regex yerine `strip_prefix` / `split_once` zinciri: her format bir önek + isim, ve kod hangi aracın hangi mesajı ürettiğini belgeliyor. `?` operatörünün `Option` üzerinde de çalıştığına dikkat — "bulamazsan `None` dön" demenin en kısa yolu. Azure DevOps'un `Merged PR 123: …` mesajında dal adı yok; o durumda şerit `Anonymous` olur.

### 3.3 Topolojik sıralama

```rust
fn topological_order(mut commits: Vec<Commit>) -> Vec<Commit> {
    // pending[i]: pencere içindeki kaç parent'ı henüz yerleşmedi
    // children[p]: p'yi parent olarak gösteren commit'ler
    // ...
    let key = |i: usize, commits: &[Commit]| Reverse((commits[i].committer.time.seconds, commits[i].id, i));
    let mut ready: BinaryHeap<_> = (0..commits.len()).filter(|&i| pending[i] == 0).map(|i| key(i, &commits)).collect();
    let mut order = Vec::with_capacity(commits.len());
    while let Some(Reverse((_, _, i))) = ready.pop() {
        order.push(i);
        for &child in &children[i] {
            pending[child] -= 1;
            if pending[child] == 0 {
                ready.push(key(child, &commits));
            }
        }
    }
    // Move commits out of the old vector without cloning them.
    let mut slots: Vec<Option<Commit>> = commits.drain(..).map(Some).collect();
    order.into_iter().map(|i| slots[i].take().expect("each index appears once")).collect()
}
```

İki Rust detayı:

- **`BinaryHeap` + `Reverse`**: Rust'ın heap'i max-heap'tir. En eski tarihi önce almak için anahtarı `Reverse` ile sarıyoruz. Tuple karşılaştırması sözlük sırasıyla yapılır: önce tarih, eşitse id — bu, sonucu deterministik yapar.
- **`Option::take` ile taşıma**: sıralanmış yeni vektörü `clone()` olmadan kurmak için her commit'i `Option` içine koyup yerinden `take()` ediyoruz. Ownership bir yerden diğerine *taşınıyor*, kopyalanmıyor.

Pencere dışındaki parent'lar `index.get(parent)` ile `None` döner ve sayılmaz — Faz 1'de trait sözleşmesine yazdığımız "callers must cope" maddesi burada karşılanıyor.

### 3.4 Aday dallar: yerel ve remote ikizleri

```rust
fn branch_candidates(refs: &[GitRef], head: &Head, priority: &BranchPriority,
                     index: &HashMap<CommitId, Column>) -> Vec<Candidate>
```

- `main` ve `origin/main` aynı adaya düşer (tek şerit, `refs: ["main", "origin/main"]`).
- Yerelde karşılığı olmayan `origin/feature/x` kendi adayıdır ama sıralamada tüm yerel dallardan sonra gelir.
- `HEAD` detached ise en sona "HEAD" adlı bir aday eklenir.

Sıralama anahtarı `(remote_only, rank, name)` tuple'ı. Aynı öncelikteki dallar ada göre sıralanır; bu da deterministik çıktı demek.

### 3.5 Şerit sahiplenme — `LaneBuilder::claim`

```rust
fn claim(&mut self, label: LaneLabel, refs: Vec<String>, tips: &[Column]) {
    let id = self.lanes.len();
    let mut claimed: Vec<Column> = Vec::new();
    for &tip in tips {
        let mut cursor = Some(tip);
        while let Some(column) = cursor {
            if self.lane_of[column].is_some() {
                break;                          // başkasının commit'i: fork noktası
            }
            self.lane_of[column] = Some(id);
            claimed.push(column);
            cursor = self.first_parent[column];  // sadece ilk parent
        }
    }
    if claimed.is_empty() {
        return;                                 // kendi commit'i olmayan dal şerit açmaz
    }
    self.lanes.push(Lane { id, label, refs, /* ... */ });
}
```

Son `if` önemli bir ürün kararı: `git branch yeni-dal` deyip hiç commit atmadıysan o dal ayrı bir hat değil; `main`'in istasyonunda bir etiket olarak görünür. Aynı şey fast-forward ile merge edilmiş dallar için de geçerli.

### 3.6 Silinmiş dallar

```rust
for column in (0..commits.len()).rev() {
    if builder.lane_of[column].is_some() { continue; }
    let label = merged_by(column, &commits)
        .and_then(|merge| branch_from_merge_message(&commits[merge].summary))
        .map(LaneLabel::Inferred)
        .unwrap_or(LaneLabel::Anonymous);
    builder.claim(label, Vec::new(), &[column]);
}
```

`Option` zinciri (`and_then` → `map` → `unwrap_or`) üç ihtimali tek ifadede ele alıyor: yutan merge yok, var ama mesajdan ad çıkmıyor, ya da ad bulundu.

### 3.7 Geçişler

```rust
for (to, commit) in commits.iter().enumerate() {
    for (n, parent) in commit.parents.iter().enumerate() {
        let Some(&from) = index.get(parent) else { continue };
        if lane_of[from] == lane_of[to] { continue; }
        let kind = if n == 0 { TransitionKind::Fork } else { TransitionKind::Merge };
        result.push(Transition { kind, from_lane: lane_of[from], from_column: from,
                                 to_lane: lane_of[to], to_column: to });
    }
    // + cherry-pick satırı
}
```

Tek kural, üç sonuç. `git pull` ile oluşan "kendi remote'unu merge etme" durumunda iki parent da aynı şeritte olduğundan geçiş üretilmez — ekranda anlamsız bir döngü çizmemiş oluruz.

### 3.8 Renk

`main`/`master` her zaman 0 numaralı renk, diğerleri paletteki 1..9 arasında döner. Renklerin kendisi ön yüzde (Faz 5); core sadece indeks verir. Satırlar şimdilik şerit başına bir tane — sıkıştırma Faz 5'te.

### 3.9 Testler

**Birim testleri** (`metro.rs` içinde) elle kurulmuş küçük graflarla çalışıyor. `testing.rs`'teki `FakeHistory` bunu üç satıra indiriyor:

```rust
#[test]
fn feature_branch_forks_and_merges_back() {
    let mut h = FakeHistory::new();
    let a = h.commit("a", &[]);
    let f1 = h.commit("f1", &[a]);
    let b = h.commit("b", &[a]);
    let m = h.commit("merge", &[b, f1]);
    h.branch("main", m);
    h.branch("feature/x", f1);
    let map = h.build();

    assert_eq!(map.lanes.len(), 2);
    let kinds: Vec<_> = map.transitions.iter().map(|t| t.kind).collect();
    assert_eq!(kinds, [TransitionKind::Fork, TransitionKind::Merge]);
}
```

Diğerleri: tek dal, öncelik kararı, `Inferred`/`Anonymous` şerit, remote ikizi, commit'siz dal, pencere dışı parent, **saati geri kalmış commit** (çocuk asla parent'tan önce gelmiyor) ve `main`'in renk sabitliği.

**Uçtan uca test** (`easy-git-repo/tests/metro_sample.rs`) gerçek fixture repo'yu gix ile okuyup planın beklentilerini doğruluyor:

```rust
assert_eq!(named, 6);
assert_eq!(inferred[0].label.text(), "feature/old-search");
assert_eq!(count(TransitionKind::Merge), 5);
assert_eq!(count(TransitionKind::CherryPick), 1);
```

**Snapshot testi** — `describe()` fonksiyonu haritayı bölümün başındaki metin çizimine çeviriyor, `insta` bunu `tests/snapshots/metro_sample__snapshot.snap` dosyasıyla karşılaştırıyor:

```rust
#[test]
fn snapshot() {
    insta::assert_snapshot!(describe(&sample_map()));
}
```

Algoritmada bir şey değişirse test kırılır ve fark satır satır gösterilir. Değişiklik bilinçliyse:

```powershell
cargo install cargo-insta   # bir kez
cargo insta review
```

Fixture sabit tarihli olduğu için (Faz 1) snapshot her makinede aynı.

## 4. Takıldığın yerler

| Belirti | Sebep / çözüm |
|---|---|
| Bir feature dalının commit'leri `develop`'ta görünüyor | O dal fast-forward ile merge edilmiş ve silinmiş; git'te iz yok. Algoritma değil, bilginin kendisi eksik. |
| `develop` ile `main` "yer değiştirmiş" gibi | Öncelik listesi. `BranchPriority::new([...])` ile değiştir; kural net: listede önce olan paylaşılan geçmişi alır. |
| `BinaryHeap` en yeniyi önce veriyor | Max-heap; anahtarı `Reverse(...)` ile sarmayı unutmuşsun. |
| Snapshot her çalıştırmada farklı | Bir yerde `HashMap` sırası çıktıya sızıyor. Çıktıya giden koleksiyonlarda `BTreeMap` ya da açık `sort` kullan (biz `refs_at` için `BTreeMap` seçtik). |
| `cannot borrow as mutable` `claim` içinde | `lanes` ve `lane_of`'u aynı struct'ta tutup `&mut self` ile değiştiriyoruz; `first_parent`'ı ise ödünç (`&'a [..]`) alıyoruz. Üçünü ayrı değişkenlerde closure içinde değiştirmeye çalışmak bu hatayı üretir. |

## 5. Ne öğrendik

- git geçmişinden "dal" çıkarımı: first-parent zinciri + öncelik.
- Kahn algoritması ile topolojik sıralama; `BinaryHeap` + `Reverse`.
- Rust'ta graf için arena/indeks kalıbı; `Option::take` ile klonsuz taşıma.
- Geçişleri tek bir kuraldan türetmek, algoritmayı adımlara bölüp her adımı ayrı test etmek.
- `insta` ile snapshot testi ve deterministik çıktının önemi.

## 6. Kendin dene

1. Öncelik listesinde `develop`'u `main`'in önüne al. Snapshot nasıl değişiyor? `cargo insta review` ile incele, sonra reddet.
2. `FakeHistory` ile bir **octopus merge** testi yaz (3 parent'lı commit). Kaç `Merge` geçişi bekliyorsun?
3. `branch_from_merge_message`'a GitLab'ın `See merge request group/project!42` satırını tanıyan bir durum ekle — bu satır özet satırında mı, gövdede mi? (İpucu: fonksiyon şimdilik sadece `summary` alıyor.)
