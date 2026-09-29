# Faz 1 — gix ile Git'i Okumak

> **Önceki:** [00 — Kurulum ve iskelet](00-kurulum-ve-iskelet.md) · **Sonraki:** [02 — Şerit algoritması](02-serit-algoritmasi.md)

## 1. Bu bölümde ne yapacağız

Diskteki bir git repository'sini **gitoxide (`gix`)** ile açıp üç şey okuyacağız: ref'ler (dallar, remote dallar, tag'ler), `HEAD` ve commit geçmişi. Bunları core crate'teki sade tiplere çevireceğiz. Çekirdek bu verinin gix'ten geldiğini bilmeyecek; arada bir trait olacak: `HistorySource`.

Testler için de planın 5.5 bölümündeki **"Metro Hattı Hikâyesi"** repo'sunu kod ile üreten bir fixture yazacağız.

## 2. Neden böyle

### git'in kısa anatomisi

| Kavram | Diskte ne | Bizim için anlamı |
|---|---|---|
| **Commit** | `.git/objects` içinde sıkıştırılmış nesne: tree, parent id'leri, yazar, committer, mesaj | İstasyon |
| **Ref** | `.git/refs/heads/main` gibi bir dosya ya da `packed-refs` içinde bir satır; içinde sadece bir commit id | Hat adı |
| **Annotated tag** | Ayrı bir *tag nesnesi*; commit'e o işaret eder | Durak tabelası — ama önce "soyulması" (peel) gerekir |
| **HEAD** | `.git/HEAD`: ya `ref: refs/heads/main` ya da doğrudan bir id | "Buradasınız" |

Kritik gözlem: **commit nesnesinde dal adı yok.** Sadece parent'lar var. "Bu commit hangi dalda" sorusu bir çıkarım; onu Faz 2'de yapacağız. Bu fazda sadece ham veriyi topluyoruz.

### Neden trait?

DockerCity'deki `IServiceCategoryResolver`'ın Rust karşılığı:

```
easy-git-core        easy-git-repo
┌─────────────────┐  ┌─────────────────────────┐
│ trait           │◄─┤ impl HistorySource      │
│ HistorySource   │  │   for GixSource         │
│                 │  └─────────────────────────┘
│ (Faz 2) şerit   │  ┌─────────────────────────┐
│ algoritması     │◄─┤ testlerde: FakeSource   │
└─────────────────┘  └─────────────────────────┘
```

Şerit algoritması `&impl HistorySource` alır. Testlerde 5 commit'lik bir graf elle kurulur, gerçek repo gerekmez. Bir gün gix'in API'si değişirse sadece `easy-git-repo` etkilenir — gix henüz 1.0 değil, bu gerçek bir risk.

### Neden `[u8; 20]`, `String` değil?

`CommitId` 20 byte'lık bir dizi. Hex string 40 byte + heap tahsisi olurdu; ayrıca `"merhaba"` gibi geçersiz bir değeri tutabilirdi. Tipi dar tutmak "geçersiz durum temsil edilemez" ilkesinin küçük bir örneği. `Copy` olduğu için ownership derdi de yok: `HashMap<CommitId, usize>` anahtarı olarak rahatça kopyalanır.

### Neden core'da `thiserror` yok?

Core crate'in bağımlılık listesi boş kalsın istedik. `SourceError` için `Display` ve `Error` impl'lerini elle yazdık; `easy-git-repo`'daki `RepoError` ise aynı işi `thiserror` ile yapıyor. İki dosyayı yan yana açınca makronun ne ürettiği görülür.

## 3. Adım adım uygulama

### 3.1 Core tipleri

`crates/easy-git-core/src/id.rs` — öne çıkanlar:

```rust
#[derive(Clone, Copy, PartialEq, Eq, Hash, PartialOrd, Ord)]
pub struct CommitId([u8; 20]);

impl CommitId {
    pub fn short(&self) -> String {
        let mut hex = self.to_hex();
        hex.truncate(7);
        hex
    }
}

impl fmt::Display for CommitId {
    fn fmt(&self, f: &mut fmt::Formatter<'_>) -> fmt::Result {
        for byte in self.0 {
            write!(f, "{byte:02x}")?;
        }
        Ok(())
    }
}

impl FromStr for CommitId { /* 40 hex karakter → 20 byte */ }
```

`Debug`'ı elle yazdık (`CommitId(a3f9c21)`): test çıktısında 20 sayılık bir dizi görmek istemeyiz.

`crates/easy-git-core/src/model.rs`:

```rust
pub struct Commit {
    pub id: CommitId,
    pub parents: Vec<CommitId>,   // 0: kök, 1: normal, 2+: merge
    pub author: Signature,
    pub committer: Signature,
    pub summary: String,
    pub message: String,
}

pub enum RefKind {
    LocalBranch,
    RemoteBranch { remote: String },
    Tag,
}

pub struct GitRef { pub name: String, pub kind: RefKind, pub target: CommitId }

pub enum Head {
    Branch { name: String, target: CommitId },
    Detached(CommitId),
    Unborn { name: String },
}
```

`Head` bir enum çünkü üç durum gerçekten farklı veriler taşıyor. C#'ta bunu nullable alanlarla yapardık; Rust'ta "dal adı var ama hedef yok" gibi saçma kombinasyonlar tip seviyesinde imkânsız.

`crates/easy-git-core/src/source.rs`:

```rust
pub trait HistorySource {
    fn refs(&self) -> Result<Vec<GitRef>, SourceError>;
    fn head(&self) -> Result<Head, SourceError>;
    /// Commits reachable from `tips`, newest first, at most `limit` of them.
    /// Parents may point outside the returned window; callers must cope.
    fn walk(&self, tips: &[CommitId], limit: usize) -> Result<Vec<Commit>, SourceError>;
}
```

`walk`'un sözleşmesindeki son satır önemli: 1.500 commit limitiyle okuduğumuzda en eski commit'lerin parent'ları pencerenin dışında kalır. Faz 2'deki algoritma bunu tolere etmek zorunda.

### 3.2 gix bağımlılığı

`crates/easy-git-repo/Cargo.toml`:

```toml
[dependencies]
easy-git-core.workspace = true
thiserror.workspace = true
gix = { version = "0.88", default-features = false, features = ["sha1", "max-performance-safe"] }

[dev-dependencies]
tempfile = "3"
```

`default-features = false` ile gix'in ağ, credential, worktree gibi bizim hiç kullanmayacağımız parçalarını kapatıyoruz; derleme süresi belirgin kısalıyor. İki özelliği açıkça istiyoruz:

- **`sha1`** — gix artık SHA-256 repo'ları da destekliyor ve hangi hash'in derleneceğini seçmemizi istiyor. Seçmezsen `Please set either the sha1 or the sha256 feature flag` derleme hatası alırsın.
- **`max-performance-safe`** — zlib ve paralel okuma için saf Rust hızlandırmaları.

### 3.3 `GixSource`

`crates/easy-git-repo/src/source.rs`:

```rust
pub struct GixSource {
    repo: gix::ThreadSafeRepository,
}

impl GixSource {
    pub fn open(path: impl AsRef<Path>) -> Result<Self, RepoError> {
        let path = path.as_ref();
        let repo = gix::discover(path)
            .map_err(|_| RepoError::NotARepository(path.display().to_string()))?;
        Ok(Self { repo: repo.into_sync() })
    }
}
```

- **`gix::discover`** klasörden yukarı doğru `.git` arar — kullanıcı repo'nun bir alt klasörünü seçse de çalışır, tıpkı `git` komutunun kendisi gibi.
- **`ThreadSafeRepository`**: `gix::Repository` thread'ler arasında paylaşılamaz (içinde önbellekler var). Faz 3'te Tauri command'ları farklı thread'lerde çalışacak, bu yüzden paylaşılabilir versiyonu saklıyor, her işte `to_thread_local()` ile yerel bir kopya alıyoruz. Bu, gix'in "paylaşılan salt okunur çekirdek + thread başına önbellek" tasarımı.

**Ref'leri okumak:**

```rust
for reference in repo.references()?.all()? {
    let mut reference = reference?;
    let full = reference.name().as_bstr().to_str_lossy().into_owned();
    let Some((kind, name)) = classify(&full) else { continue };

    // Annotated tags point at a tag object; peel until we reach the commit.
    let Ok(id) = reference.peel_to_id() else { continue };
    let Ok(object) = id.object() else { continue };
    let Ok(commit) = object.peel_to_commit() else { continue };

    refs.push(GitRef { name, kind, target: commit_id(&commit.id) });
}
```

`classify` saf bir fonksiyon — `refs/heads/…` → yerel dal, `refs/remotes/origin/…` → remote dal, `refs/tags/…` → tag. `refs/stash` ve `refs/remotes/origin/HEAD` gibi ref'ler `None` döner ve atlanır (planın 3.1-7. maddesi). Saf olduğu için kendi birim testi var.

`let … else` (Rust 1.65+) burada çok işe yarıyor: "bu adımlardan biri başarısız olursa bu ref'i atla" mantığını iç içe `match` olmadan yazıyoruz.

**HEAD:**

```rust
let head = repo.head()?;
let short = head.referent_name().map(|n| n.shorten().to_str_lossy().into_owned());
if head.is_unborn() {
    return Ok(Head::Unborn { name: short.unwrap_or_default() });
}
let id = head.id().map(|id| commit_id(&id));
Ok(match (short, id) {
    (Some(name), Some(target)) => Head::Branch { name, target },
    (None, Some(target)) => Head::Detached(target),
    (name, None) => Head::Unborn { name: name.unwrap_or_default() },
})
```

Tuple üzerinde `match` ile dört kombinasyonu tek bakışta okunur halde ele alıyoruz.

**Commit yürüyüşü:**

```rust
let walk = repo
    .rev_walk(tips.iter().copied().map(object_id))
    .sorting(gix::revision::walk::Sorting::ByCommitTime(Default::default()))
    .all()?;

for info in walk.take(limit) {
    let info = info?;
    let object = info.object()?;
    let decoded = object.decode()?;
    let author = decoded.author()?;       // gix 0.88: imza ayrıştırma da hata dönebilir
    // ...
}
```

- Birden fazla uçtan (tüm dal ve tag'ler) aynı anda başlıyoruz; gix her commit'i bir kez verir.
- `ByCommitTime` en yeniden eskiye doğru gider; `take(limit)` ile "son N commit" penceresini alıyoruz. Topolojik sıralamayı Faz 2'de core kendisi yapacak.
- Iterator zincirinin tembel (lazy) olduğuna dikkat: `take(5)` dersen gix sadece 5 commit okur.

### 3.4 Hata tipleri

`crates/easy-git-repo/src/error.rs`:

```rust
#[derive(Debug, thiserror::Error)]
pub enum RepoError {
    #[error("'{0}' is not inside a git repository")]
    NotARepository(String),
    #[error("could not read references: {0}")]
    References(String),
    #[error("could not walk history: {0}")]
    Walk(String),
    #[error("could not decode object {id}: {detail}")]
    Decode { id: String, detail: String },
}

impl From<RepoError> for SourceError { /* ... */ }
```

`From` impl'i sayesinde trait metotlarında `Ok(self.read_refs()?)` yazmak yeterli: `?` operatörü `RepoError`'ı otomatik olarak `SourceError`'a çevirir.

### 3.5 Fixture: "Metro Hattı Hikâyesi"

`crates/easy-git-repo/src/fixture.rs` git komut satırını çağırarak repo'yu kurar. Deterministik olması için üç hile:

```rust
self.clock += 3600;                                  // her git çağrısı bir saat sonra
let date = format!("@{} +0300", self.clock);
Command::new("git")
    .args(["-c", "init.defaultBranch=main", "-c", "commit.gpgsign=false", /* ... */])
    .env("GIT_CONFIG_GLOBAL", &self.config)          // kullanıcının ~/.gitconfig'i karışmasın
    .env("GIT_CONFIG_NOSYSTEM", "1")
    .env("GIT_AUTHOR_DATE", &date)
    .env("GIT_COMMITTER_DATE", &date)
    // ...
```

1. **Sabit tarih** → aynı içerik + aynı tarih + aynı yazar = **aynı commit id**. Her makinede, her çalıştırmada.
2. **İzole config** → senin global `commit.gpgsign=true` ayarın ya da farklı varsayılan dal adın testi bozmasın.
3. **Her commit ayrı dosyaya yazar** → merge'ler asla çakışmaz.

Hikâyenin kendisi okunur kalsın diye küçük yardımcılar var:

```rust
b.branch_from("feature/login", "develop")?;
b.commit("src/login/form.txt", "Add login form")?;
// ...
b.merge_no_ff("develop", "feature/login")?;
```

Aynı kod elle deneme için bir **example** olarak da çalışıyor:

```powershell
cargo run -p easy-git-repo --example make-sample-repo -- C:\temp\easy-git-sample
```

> Planda bunun için ayrı bir PowerShell betiği öngörmüştük. Rust example'ı tercih ettik: tek kaynak, testlerle birebir aynı repo, Windows/Linux fark etmez.

### 3.6 Testler

`crates/easy-git-repo/tests/read_sample.rs` — entegrasyon testleri (her biri geçici klasörde fixture'ı yeniden kurar):

```rust
#[test]
fn walks_every_commit_once() {
    let (_root, source) = open_sample();
    let tips: Vec<_> = source.refs().unwrap().iter().map(|r| r.target).collect();
    let commits = source.walk(&tips, 1000).unwrap();

    assert_eq!(commits.len(), SAMPLE_COMMIT_COUNT);            // 20
    assert_eq!(commits.iter().filter(|c| c.is_merge()).count(), 5);
    assert_eq!(commits.iter().filter(|c| c.is_root()).count(), 1);
    assert_eq!(commits[0].summary, "Update changelog");        // en yeni önce
}
```

Diğer testler: yerel/remote dal ve tag listesi, annotated tag'in doğru commit'e soyulması, `HEAD`'in `main`'de olması, limitin uygulanması, cherry-pick satırının mesajda korunması ve git olmayan bir klasörde anlaşılır hata.

`_root` değişkenine dikkat: `TempDir` drop edildiğinde klasör silinir. `_` ile başlayan ama *isimli* bir değişken değer testin sonuna kadar yaşar; sadece `_` yazsaydık klasör hemen silinirdi — klasik bir Rust tuzağı.

```powershell
cargo test -p easy-git-repo
```

## 4. Takıldığın yerler

| Belirti | Sebep / çözüm |
|---|---|
| `Please set either the sha1 or the sha256 feature flag` | `default-features = false` ile hash seçimi de kapandı; `features = ["sha1", …]` ekle. |
| `expected SignatureRef, found Result<…>` | gix 0.88'de `author()`/`committer()` imzayı ayrıştırırken hata dönebiliyor; `?` ile aç. |
| Fixture testlerinde `git … failed` | `git` PATH'te değil ya da çok eski. `git --version` ile kontrol et. |
| `Repository` `Send` değil hatası | Paylaşılan yerde `ThreadSafeRepository` tut, kullanırken `to_thread_local()`. |
| Tag'ler eksik geliyor | Annotated tag commit'e değil tag nesnesine işaret eder; `peel_to_commit` adımı atlanmış. |

## 5. Ne öğrendik

- git'in nesne modeli: commit dal bilgisi taşımaz, ref sadece bir işaretçidir.
- Trait ile soyutlama ve "adaptör crate" fikri; gix'in etkisini tek crate'e hapsetmek.
- `[u8; 20]` ile dar tip, `Display`/`FromStr` impl'leri.
- Elle hata tipi vs `thiserror`; `From` ile `?` dönüşümü.
- `let … else`, tuple `match`, tembel iterator'lar.
- Deterministik test verisi: sabit tarih ve izole config ile tekrarlanabilir commit id'leri.

## 6. Kendin dene

1. Fixture'a 11. adım olarak bir **octopus merge** ekle (`git merge a b`). `walk` testinde merge sayısı kaça çıkıyor?
2. `GixSource`'a `fn is_shallow(&self) -> bool` ekle (ipucu: gix'te `repo.is_shallow()`).
3. `classify` fonksiyonuna `refs/notes/` ref'lerini yok sayan bir test yaz — kod zaten doğru mu?
