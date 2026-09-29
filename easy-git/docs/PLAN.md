# easy-git — Proje Plan Dokümanı

> **Atölye:** Fikirler Atölyesi
> **Doküman sürümü:** v1.0 — 29 Eylül 2026
> **Dokümanlar:** bu klasör (`easy-git/docs`)
> **Kod:** `C:\Users\burak\Development\coding-vibes\easy-git`
> **Durum:** Taslak — onay bekliyor

---

## 1. Tek Cümlelik Tanım

Yerel bir git repository'sini okuyup dalları (branch) **yatay eksende, soldan sağa akan şeritler** olarak gösteren; dallar arası geçişleri (ayrılma, birleşme, cherry-pick) farklı renklerle vurgulayan, salt okunur bir Windows masaüstü dashboard'u.

## 2. Atölyenin Amacı

| Katman | Hedef |
|---|---|
| **Ürün** | Git Extensions / VS Code graf görünümünden daha sade, "hangi dal nereden çıktı, nereye aktı" sorusuna tek bakışta cevap veren bir izleme aracı |
| **Öğreti** | Rust (ownership, trait, enum, hata yönetimi, workspace), gitoxide (`gix`) ile git iç yapısı, graf algoritmaları, Tauri 2 (command, state, plugin, capability), Svelte + TypeScript ile SVG tabanlı görselleştirme |

Öncelik yine **öğreti** tarafında. Her faz DockerCity'deki gibi "ne yapacağız → neden böyle → kod → ne öğrendik" akışında ayrı bir doküman olarak üretilecek.

**Kapsam kararı:** Uygulama **salt okunur**. Checkout, merge, fetch gibi repo'yu değiştiren hiçbir işlem yapılmaz. Bu hem güvenli hem de odağı görselleştirmede tutar.

---

## 3. Ürün Konsepti: Metro Haritası Metaforu

DockerCity'de şehir vardı; burada **metro haritası** var. Metro haritaları tam da bu problem için icat edildi: karmaşık bir ağı, coğrafyayı değil *bağlantıları* öne çıkararak okunur kılmak.

| Git kavramı | Metro karşılığı | Görsel ifade |
|---|---|---|
| Repository | **Metro ağı** | Tuvalin tamamı |
| Branch | **Hat** | Yatay şerit, kendine ait renk |
| Commit | **İstasyon** | Hat üzerinde daire |
| Merge commit | **Aktarma istasyonu** | Çift halkalı, daha büyük daire |
| Dalın ayrılması (fork) | **Hattın ayrıldığı makas** | Üst hattan alt hatta inen eğri |
| Merge | **Hatların birleştiği makas** | Kaynak hattan hedef hatta çıkan, ok başlı eğri |
| Cherry-pick | **Transfer** | Kesikli eğri, ayrı renk |
| Tag | **Durak tabelası** | İstasyon üstünde bayrak |
| `HEAD` | **"Buradasınız" işareti** | Parlayan halka + rozet |
| Silinmiş ama merge edilmiş dal | **Kapatılmış hat** | Soluk renk, adı merge mesajından çıkarılır |
| Remote-tracking dal (`origin/x`) | **Hattın "resmi" güzergâhı** | Yerel dal ile aynı şeritte küçük bulut rozeti |

**Eksenler:**

- **X ekseni (soldan sağa):** zaman akışı. Varsayılan mod *topolojik sıra* — her commit kendi sütununda, eskiler solda, yeniler sağda. Faz 8'de alternatif *tarih ölçekli* mod eklenir.
- **Y ekseni:** hatlar. En üstte `main`/`master`, altında `develop`, sonra `release/*`, `hotfix/*`, `feature/*` ve diğerleri.
- Uygulama açıldığında görünüm **en sağa (en güncel commit'lere)** kaydırılmış gelir.

### 3.1 Git'in kafa karıştıran gerçekleri (planın kenar durumları)

DockerCity'deki "7 kenar durum" listesinin karşılığı. Git ile çalışan her görselleştiricinin bunlarla yüzleşmesi gerekiyor:

1. **Commit hangi dala ait olduğunu bilmez.** Git'te dal sadece bir commit'e işaret eden hareketli bir etikettir. "Bu commit `feature/login` dalında yapıldı" bilgisi hiçbir yerde saklanmaz — **çıkarım yapmamız gerekir.** Planın kalbi bu çıkarımı yapan şerit algoritması (bkz. 5.3).
2. **Merge edilip silinmiş dallar.** Commit'leri duruyor ama onları işaret eden ref yok. Ad, merge commit mesajından çıkarılabilir (`Merge branch 'feature/x'`, `Merge pull request #42 from user/feature/x`); çıkarılamazsa "adsız hat" olarak gösterilir.
3. **Fast-forward merge iz bırakmaz.** Merge commit oluşmadığı için dalın varlığı tarihte görünmez; commit'ler hedef dalın düz çizgisinde kalır. Bu bir hata değil, dokümanda açıklanacak bir sınır.
4. **Birden fazla parent.** Normal merge 2, octopus merge 3+ parent'a sahiptir. Her ek parent ayrı bir birleşme geçişi üretir.
5. **Aynı commit'i işaret eden birden fazla ref.** `main` ile `origin/main` aynı yerdeyken tek şerit gösterilmeli; ayrıştıklarında ahead/behind bilgisi verilmeli.
6. **Zaman güvenilmez.** Author date ile committer date farklı olabilir, rebase ile tarihler karışır, makine saatleri yanlış olabilir. X ekseni bu yüzden *tarihe göre değil topolojiye göre* sıralanır; tarih sadece eşitlikte kullanılır.
7. **Detached HEAD, tag'ler, stash.** HEAD bir dala değil doğrudan commit'e işaret edebilir. Stash ref'leri (`refs/stash`) varsayılan olarak gizlenir.
8. **Büyük repolar.** On binlerce commit'lik bir repoyu tek seferde çizmek anlamsız. Varsayılan limit: son **1.500 commit** (ayarlanabilir), "daha eskisini yükle" ile genişletilir.
9. **Cherry-pick tespiti.** Git bunu kaydetmez; sadece `git cherry-pick -x` ile eklenen `(cherry picked from commit <hash>)` satırı üzerinden güvenilir tespit yapılabilir. Diğer durumlar gösterilmez.
10. **Eğik çizgili dal adları.** `feature/login`, `feature/payments` gibi adlar kenar çubuğunda klasör gibi gruplanır.

---

## 4. Teknoloji Yığını

| Alan | Seçim | Not |
|---|---|---|
| Çekirdek dil | **Rust** (stable, MSVC toolchain) | Edition 2024 |
| Masaüstü kabuğu | **Tauri 2** | WebView2 üzerinde çalışır, Windows 11'de hazır gelir |
| Git erişimi | **`gix` (gitoxide)** | Saf Rust, C bağımlılığı yok; git nesne modelini öğrenmek için ideal |
| Ön yüz | **Svelte 5 + TypeScript + Vite** | Reaktif state, az boilerplate |
| Çizim | **SVG** (Svelte bileşenleri) | Binlerce eleman için yeterli; performans sorun olursa Canvas'a geçiş yolu açık (bkz. Faz 8) |
| Rust ↔ TS tip paylaşımı | **`ts-rs`** | Rust DTO'larından TypeScript tipleri üretilir — iki taraf elle senkron tutulmaz |
| Serileştirme | `serde` + `serde_json` | |
| Hata yönetimi | `thiserror` (kütüphaneler), `anyhow` sadece testlerde | |
| Dosya izleme | `notify` (Faz 7) | `.git/refs`, `HEAD`, `packed-refs` değişikliklerini izler |
| Ayarlar / son repolar | `tauri-plugin-store` | Küçük JSON deposu; DockerCity'deki SQLite burada gereksiz |
| Klasör seçici | `tauri-plugin-dialog` | |
| Test | `cargo test` + `insta` (snapshot) | Ön yüz için Vitest (Faz 4+) |

### 4.1 Faz 0'da doğrulanacak detaylar

- Rust MSVC toolchain + Visual Studio Build Tools (C++ workload) kurulu mu?
- Node.js LTS ve npm sürümü.
- `create-tauri-app` şablonunun Svelte-TS çıktısı ile Cargo workspace yapısının uyumu.
- `gix` için gereken feature bayrakları (varsayılan feature seti çok geniş; sadece ihtiyaç duyulanlar açılacak, derleme süresi kısalır).

*Kesin sürüm numaraları Faz 0'da kurulum sırasında doğrulanacak — bugünden çivilenmiyor.*

---

## 5. Mimari

### 5.1 Klasör yapısı

```
coding-vibes/easy-git/
├── Cargo.toml                     # Cargo workspace kökü
├── package.json                   # Vite + Svelte + Tauri CLI
├── README.md
├── docs/
│   ├── PLAN.md                    # bu doküman
│   └── tutorial/                  # 00-…, 01-… fazlar
├── crates/
│   ├── easy-git-core/             # Domain: Commit, Branch, Lane, Transition + şerit algoritması
│   │                              # git'ten ve Tauri'den habersiz, saf Rust
│   └── easy-git-repo/             # gix adaptörü: diskteki repo → core modeli
├── src-tauri/                     # Tauri uygulaması: command'lar, state, plugin'ler
│   ├── capabilities/
│   └── src/
├── src/                           # Svelte + TypeScript ön yüz
│   ├── lib/
│   │   ├── graph/                 # SVG bileşenleri: Lane, Station, Transition, Legend
│   │   ├── panels/                # Kenar çubuğu, detay paneli, tooltip
│   │   └── bindings/              # ts-rs'nin ürettiği tipler
│   └── App.svelte
└── tests/fixtures/                # Test repo senaryoları (bkz. 5.5)
```

**Bağımlılık yönü:** `src-tauri → easy-git-repo → easy-git-core`. Core hiçbir şeye bağımlı değil.

Bu DockerCity'deki `Domain` / `Parsing` ayrımının Rust karşılığı: şerit algoritması `gix`'i hiç görmez, bir trait üzerinden veri alır. Böylece algoritma testleri gerçek repo gerektirmeden, bellek içi kurulan küçük graflarla milisaniyelerde koşar.

### 5.2 Core domain modeli

```rust
// Değer tipleri
pub struct CommitId([u8; 20]);            // Display ile kısa/uzun hex
pub struct Signature { name: String, email: String, time: Timestamp }

pub struct Commit {
    pub id: CommitId,
    pub parents: Vec<CommitId>,           // 0: root, 1: normal, 2+: merge
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

pub enum Head { Branch(String), Detached(CommitId), Unborn }

// Veri kaynağı soyutlaması — Dependency Inversion
pub trait HistorySource {
    fn refs(&self) -> Result<Vec<GitRef>, SourceError>;
    fn head(&self) -> Result<Head, SourceError>;
    fn walk(&self, tips: &[CommitId], limit: usize) -> Result<Vec<Commit>, SourceError>;
}
```

**Şerit modeli (algoritmanın çıktısı):**

```rust
pub enum LaneLabel {
    Named(String),       // yaşayan bir ref var
    Inferred(String),    // merge mesajından çıkarıldı (silinmiş dal)
    Anonymous,           // hiçbir ipucu yok
}

pub struct Lane { pub id: LaneId, pub label: LaneLabel, pub row: u16, pub color: u8 }

pub enum TransitionKind { Fork, Merge, CherryPick }

pub struct Transition {
    pub kind: TransitionKind,
    pub from: (LaneId, CommitIdx),
    pub to:   (LaneId, CommitIdx),
}

pub struct MetroMap {
    pub commits: Vec<Commit>,             // topolojik sıra, eskiden yeniye
    pub column_of: Vec<u32>,              // commit → X sütunu
    pub lane_of: Vec<LaneId>,             // commit → şerit
    pub lanes: Vec<Lane>,
    pub transitions: Vec<Transition>,
    pub refs_at: HashMap<CommitIdx, Vec<GitRef>>,
    pub head: Head,
}
```

**Rust öğretisi açısından kritik karar:** Graf yapıları Rust'ta `Rc<RefCell<Node>>` ile kurulmaya çalışıldığında ownership kavgasına dönüşür. Burada **indeks tabanlı (arena)** yaklaşım kullanılacak: commit'ler bir `Vec` içinde, ilişkiler `usize` indeksleriyle. Hem borrow checker ile barışık hem de hızlı. Faz 2'nin ana derslerinden biri.

### 5.3 Şerit (lane) algoritması

Planın en özgün kısmı — "commit hangi dala ait" sorusunun cevabı.

1. **Ref önceliklendirme.** Dallar bir öncelik listesine göre sıralanır (ayarlanabilir):
   `main`/`master` → `develop` → `release/*` → `hotfix/*` → `feature/*` → diğer yerel dallar → sadece remote'ta olan dallar.
2. **First-parent yürüyüşü.** Sırayla her dalın ucundan başlayıp *sadece ilk parent'ı* takip ederek geriye yürünür. Henüz şeridi olmayan her commit bu dalın şeridine atanır. Zaten atanmış bir commit'e çarpıldığında durulur — **o commit dalın ayrıldığı noktadır → `Fork` geçişi.**
3. **Sahipsiz commit'ler.** Adımlar bittiğinde şeritsiz kalan commit'ler, sadece merge'lerin ikinci parent'ı üzerinden erişilebilen, yani **silinmiş dallara** ait commit'lerdir. Bunlar için yeni şerit açılır; ad merge mesajından çıkarılır (`Inferred`), çıkarılamazsa `Anonymous`.
4. **Merge geçişleri.** Her merge commit'in 2. ve sonraki parent'ları için, parent'ın şeridinden merge commit'in şeridine bir `Merge` geçişi üretilir.
5. **Cherry-pick geçişleri.** Mesajında `(cherry picked from commit <hash>)` geçen ve kaynak commit'i görünür aralıkta olan commit'ler için `CherryPick` geçişi.
6. **Satır (row) sıkıştırma.** Ömrü biten bir şeridin satırı, sonra başlayan ve çakışmayan başka bir şeride verilebilir. Böylece 40 dallı bir repo 40 satır yüksekliğinde olmaz. *(Faz 5'te; MVP'de her şerit kendi satırında.)*

X sütunu = topolojik sıradaki indeks; eşitlikte committer tarihi.

### 5.4 Tauri köprüsü

```rust
#[tauri::command] async fn open_repository(path: String, state: State<'_, AppState>) -> Result<RepoSummary, AppError>;
#[tauri::command] async fn get_metro_map(options: MapOptions, state: State<'_, AppState>) -> Result<MetroMapDto, AppError>;
#[tauri::command] async fn get_commit_details(id: String, state: State<'_, AppState>) -> Result<CommitDetailsDto, AppError>;
#[tauri::command] async fn get_branch_stats(state: State<'_, AppState>) -> Result<Vec<BranchStatsDto>, AppError>;
```

- `AppState` → `Mutex<Option<OpenRepo>>`. Açık repo tek; açık repoyu değiştirmek state'i değiştirir.
- DTO'lar core tiplerinden ayrı: core `[u8; 20]` taşır, DTO hex string. DockerCity'deki DTO/Domain ayrımının ters yönü — bu sefer domain'den dışarıya.
- `AppError` `serde::Serialize` uygular, ön yüzde anlamlı mesaj olarak görünür.
- Ağır iş (repo yürüyüşü) `spawn_blocking` ile UI thread'inden uzak tutulur.
- **Capabilities:** Tauri 2'de ön yüz yalnızca izin verilen command ve plugin'lere erişir. Dialog plugin'i için `dialog:allow-open` açıkça verilir. Güvenlik modelinin öğretildiği yer Faz 3.

### 5.5 Test repo senaryosu (fixture)

DockerCity'deki `samples/docker-compose.yml`'ın karşılığı. Test sırasında `git` CLI ile **sabit tarih ve yazar bilgisiyle** geçici bir repo oluşturulur — sabit tarih = sabit hash, yani sonuçlar deterministik ve snapshot testine uygun.

Senaryo ("Metro Hattı Hikâyesi"):

| # | Adım | Test ettiği kenar durum |
|---|---|---|
| 1 | `main` üzerinde 2 commit | Kök, düz hat |
| 2 | `main`'den `develop` ayrılır, 1 commit | Fork |
| 3 | `develop`'tan `feature/login` (3 commit) → `--no-ff` ile `develop`'a merge | Normal merge |
| 4 | `develop`'tan `feature/payments` (2 commit), merge edilmez | Açık dal |
| 5 | `main`'den `hotfix/crash` (1 commit) → hem `main` hem `develop`'a merge | Bir daldan iki hedefe merge |
| 6 | `develop`'tan `feature/old-search` (2 commit) → merge → **dal silinir** | `Inferred` şerit |
| 7 | `develop`'tan `feature/typo` (1 commit) → **fast-forward** merge → silinir | İz bırakmayan merge |
| 8 | `develop`'tan `release/1.0` (1 commit: sürüm artırımı) → `main`'e merge, `v1.0` tag'i | Tag, release akışı |
| 9 | `feature/payments`'tan bir commit `main`'e `cherry-pick -x` | CherryPick |
| 10 | `origin` olarak ikinci bir bare repo, `main` push edilir, sonra yerelde 1 commit daha | Remote-tracking, ahead 1 |

Beklenen: 6 isimli (`main`, `develop`, `feature/login`, `feature/payments`, `hotfix/crash`, `release/1.0`) + 1 çıkarımlı şerit = 7 şerit, 5 merge geçişi, 1 cherry-pick geçişi, `main` origin'e göre 1 ahead.

Aynı senaryo `tests/fixtures/make-sample-repo.ps1` olarak da verilir — manuel denemelerde uygulamayla açılacak örnek repo.

---

## 6. Görsel Tasarım Kararları

| Konu | Karar |
|---|---|
| Zemin | Düz nötr ton (koyu: `#1E1E1E`, açık: `#FAFAFA`). Sütun başına çok hafif dikey kılavuz çizgisi. |
| Şerit | 4px kalınlığında yatay çizgi, dalın ilk commit'inden son commit'ine kadar. Satır yüksekliği 36px. |
| Şerit etiketi | Solda **yapışkan (sticky)** etiket sütunu — yatay kaydırırken dal adları hep görünür. |
| İstasyon (commit) | 10px daire, şerit renginde dolgu. Merge commit: 14px, çift halka. |
| HEAD | Pulse animasyonlu dış halka + "HEAD" rozeti. |
| Tag | İstasyonun üstünde küçük bayrak + ad. |
| Geçiş eğrileri | Cubic Bezier, sütun genişliğinin yarısı kadar yatay kontrol noktası; merge'de hedefe ok başı. |
| Şerit renkleri | 10 renklik, koyu ve açık temada ayırt edilebilir palet; `main` her zaman aynı renk (sabit), diğerleri sırayla. |
| Silinmiş dal şeridi | Kendi renginin %40 opaklığı, etiket italik + "(silinmiş)" |

### 6.1 Geçiş renklendirme

Geçişlerin farklı renklerde gösterilmesi için iki mod, üst çubukta tek tıkla değişir:

| Mod | Fork | Merge | Cherry-pick |
|---|---|---|---|
| **Türe göre** *(varsayılan)* | Yeşil, düz | Mor, düz, ok başlı | Turuncu, kesikli |
| **Kaynak dala göre** | Yeni dalın rengi | Kaynak dalın rengi | Kaynak dalın rengi, kesikli |

Sağ alt köşede her zaman görünen küçük bir **lejant** bulunur. Geçişin üzerine gelince iki uç istasyon ve iki şerit vurgulanır, diğer her şey soluklaşır — "bu merge nereden geldi" sorusunun tek hareketle cevabı.

### 6.2 Ekran düzeni

```
┌──────────────────────────────────────────────────────────────────────┐
│ [📂 Repo seç ▾]  coding-vibes  ·  HEAD: develop   [Türe göre|Dala göre] ⟳ │
├───────────────┬──────────────────────────────────────────────────────┤
│ DALLAR        │  main      ●────────●──────────────◎────────●        │
│ ● main   ↑1   │                     ╲             ╱                  │
│ ● develop     │  develop            ●────●───◎───●────◎─────●        │
│ ▾ feature/    │                           ╲  ╱          ╱            │
│   ● login  ✓  │  feature/login             ●─●─●        ╱             │
│   ● payments  │  feature/payments               ●──────●             │
│ ● hotfix/…  ✓ │                                                      │
│ ◌ old-search  │                                        [lejant]      │
├───────────────┴──────────────────────────────────────────────────────┤
│ a3f9c21 · Burak · 2 gün önce · "Add login form validation"           │
│ Parent: 7be01d4  ·  Dallar: feature/login, develop  ·  Tag: –         │
└──────────────────────────────────────────────────────────────────────┘
```

---

## 7. Bilgilendirici Detaylar

"Hem bilgilendirici hem sade" dengesini kurmak için bilgi üç katmana ayrılır — hepsi aynı anda ekranda değil:

**Katman 1 — Her zaman görünür (kenar çubuğu):**
- Dal adı ve şerit rengi
- Varsayılan dala göre **ahead / behind** sayısı (`↑3 ↓12`)
- Merge edildi mi? (`✓`)
- Remote ile senkron durumu (bulut rozeti, `origin`'e göre ahead/behind)
- **Bayat dal** uyarısı: 30 günden uzun süredir commit almamış, merge edilmemiş dal (eşik ayarlanabilir)

**Katman 2 — Hover (tooltip):**
- Commit: kısa hash, yazar, göreli tarih, özet satırı
- Geçiş: tür, kaynak → hedef dal, tarih

**Katman 3 — Tıklama (alt detay paneli):**
- Tam hash (kopyala butonu), yazar ve committer ayrı ayrı, mutlak tarih
- Tam commit mesajı
- Parent'lar (tıklanabilir — istasyona atlar)
- Bu commit'i içeren dallar ve tag'ler
- Dal seçildiğinde: ilk commit, son aktivite, commit sayısı, katkıda bulunan yazarlar

---

## 8. Yol Haritası

MVP = **Faz 0 → Faz 6**. Sonrası cila ve genişleme.

### Faz 0 — Atölye kurulumu
`docs/tutorial/00-kurulum-ve-iskelet.md`

- Rust (MSVC), Visual Studio Build Tools, Node.js LTS, WebView2 kontrolü
- `create-tauri-app` ile Svelte + TypeScript iskeleti
- Kökte Cargo workspace: `src-tauri`, `crates/easy-git-core`, `crates/easy-git-repo`
- `npm run tauri dev` ile boş pencere, `cargo test --workspace` yeşil

**Kazanım:** Tauri'nin iki süreçli yapısı (Rust core + WebView), Cargo workspace, `tauri.conf.json` anatomisi, geliştirme döngüsü.
**Çıktı:** Boş pencere açılıyor, workspace derleniyor.

---

### Faz 1 — Git'i okumak
`docs/tutorial/01-gix-ile-git-okuma.md`

- `gix::open` ile repo açma, hata durumları (repo değil, bozuk, bare)
- Ref'leri listeleme: yerel dal, remote dal, tag; `HEAD` çözümleme (dal / detached / unborn)
- Commit yürüyüşü: çoklu uçtan başlayan, limitli, topolojik sıralı walk
- `HistorySource` trait'inin `GixSource` ile uygulanması
- Fixture repo üreticisi ve ilk testler

**Kazanım:** Git nesne modeli (commit, tree, ref, packed-refs), `gix` API'si, trait ile soyutlama, `thiserror` ile hata tipleri, `Result` zincirleme.
**Çıktı:** Test, fixture repodaki tüm ref'leri ve commit'leri doğru sayıyla okuyor.

---

### Faz 2 — Şerit algoritması
`docs/tutorial/02-serit-algoritmasi.md`

- Core domain tipleri, indeks tabanlı (arena) graf
- Bölüm 5.3'teki algoritmanın adım adım uygulanması
- Merge mesajından dal adı çıkarımı (regex yerine elle yazılmış küçük parser — öğretici)
- Bellek içi `FakeSource` ile birim testleri + fixture repo ile `insta` snapshot testi

**Kazanım:** Rust'ta graf modelleme, neden `Rc<RefCell>` değil de indeks, enum ile durum modelleme, `HashMap`/`HashSet` kullanımı, snapshot testing.
**Çıktı:** Fixture repodan beklenen `MetroMap` üretiliyor: 7 şerit, 5 merge, 1 cherry-pick.

---

### Faz 3 — Tauri köprüsü ve repo seçimi
`docs/tutorial/03-tauri-kopru.md`

- `AppState`, command'lar, `spawn_blocking`
- DTO'lar ve `ts-rs` ile TypeScript tip üretimi
- `tauri-plugin-dialog` ile klasör seçimi, `.git` doğrulaması
- `tauri-plugin-store` ile son açılan repolar listesi
- Capabilities / permission yapılandırması

**Kazanım:** Tauri 2 command modeli, paylaşılan state ve `Mutex`, async/blocking ayrımı, IPC serileştirme, Tauri güvenlik modeli.
**Çıktı:** Klasör seçilince ön yüz konsolunda repo özeti ve harita JSON'u görünüyor.

---

### Faz 4 — İlk görselleştirme
`docs/tutorial/04-svg-ile-cizim.md`

- Svelte 5 rune'ları (`$state`, `$derived`) ile uygulama state'i
- `MetroView.svelte`: SVG tuvali, sütun/satır → piksel dönüşümü
- Şeritlerin ve istasyonların çizimi, yapışkan etiket sütunu
- Yatay kaydırma, açılışta en sağa konumlanma

**Kazanım:** SVG koordinat sistemi, Svelte reaktivitesi, bileşen ayrıştırma, `{#each}` ile keyed listeler.
**Çıktı:** Fixture repo açılınca dallar yatay şeritler olarak görünüyor.

---

### Faz 5 — Geçişler ve renkler
`docs/tutorial/05-gecisler-ve-renkler.md`

- Bezier eğrisi hesabı, ok başları (`<marker>`)
- Renk paleti (koyu/açık tema), `main` için sabit renk
- İki renklendirme modu + lejant
- Geçiş hover'ında vurgulama / soluklaştırma
- Satır sıkıştırma (5.3 — adım 6)

**Kazanım:** Eğri geometrisi, CSS değişkenleri ile tema, SVG katman sırası, UI state'inden türetilmiş görsel durum.
**Çıktı:** Fork, merge ve cherry-pick'ler farklı renklerde; lejant ve mod değişimi çalışıyor.

---

### Faz 6 — Etkileşim ve bilgi panelleri *(MVP tamamlanır)*
`docs/tutorial/06-etkilesim-ve-paneller.md`

- Hover tooltip (commit + geçiş)
- Tıklama → alt detay paneli, parent'a atlama
- Kenar çubuğu: klasör gruplaması, ahead/behind, merged, bayat dal rozeti
- Dal gizle/göster, dala tıklayınca şeride kaydırma
- `get_commit_details` ve `get_branch_stats` command'ları (Rust tarafında merge-base hesabı)

**Kazanım:** Merge-base ve ahead/behind hesabı, olay yönetimi, bileşenler arası state paylaşımı.
**Çıktı:** **Çalışan MVP.** Repo seç, metro haritasını gör, dalları ve geçişleri incele.

---

### Faz 7 — Canlı izleme
`docs/tutorial/07-canli-izleme.md`

`notify` ile `.git` altındaki ref değişikliklerini izleme, debounce, Tauri event'i ile ön yüze bildirme, yeni commit'lerin sağdan kayarak gelen animasyonu. Terminalde commit atınca dashboard kendiliğinden güncellenir.

**Kazanım:** Dosya sistemi olayları, kanal (`mpsc`) ile thread'ler arası iletişim, Tauri event sistemi.

---

### Faz 8 — Büyük repolar ve zaman ekseni
`docs/tutorial/08-buyuk-repolar.md`

Görünür alana göre sanallaştırma (sadece ekrandaki sütunlar çizilir), "daha eskisini yükle", zoom, tarih ölçekli X ekseni modu (günler/haftalar ızgarası), performans ölçümü. Gerekirse SVG → Canvas geçişi değerlendirmesi.

**Kazanım:** Viewport hesabı, sanallaştırma, profil çıkarma (`cargo flamegraph`, tarayıcı performans sekmesi).

---

### Faz 9 — Kullanılabilirlik
`docs/tutorial/09-kullanilabilirlik.md`

Commit/yazar/mesaj araması, tarih ve yazar filtresi, klavye ile gezinme, açık/koyu tema, SVG/PNG dışa aktarma, öncelik listesinin ayarlar ekranından düzenlenmesi, Tauri bundler ile MSI/NSIS kurulum paketi.

---

### Faz 10 — İleri seviye *(opsiyonel)*
`docs/tutorial/10-coklu-repo.md`

Birden fazla repoyu aynı anda izleyen özet ekranı ("hangi repomda bayat dal var?"), worktree desteği, dal karşılaştırma görünümü (iki dal arasındaki commit farkı).

---

## 9. Doküman Düzeni

DockerCity ile aynı kalıp:

1. **Bu bölümde ne yapacağız**
2. **Neden böyle** — alternatifler ve tercih gerekçesi
3. **Adım adım uygulama** — tam kod blokları, dosya yollarıyla
4. **Takıldığın yerler** — bilinen tuzaklar ve çözümleri
5. **Ne öğrendik**
6. **Kendin dene** — 2-3 küçük görev

Dosya adlandırma: `NN-konu-adi.md`, `docs/tutorial/` altında. Dokümanlar Türkçe, kod içi metinler ve yorumlar İngilizce.

---

## 10. MVP Kabul Kriterleri

- [ ] Klasör seçici ile yerel bir repo açılabiliyor; git reposu olmayan klasörde anlaşılır hata veriliyor
- [ ] Fixture repo açıldığında 6 isimli + 1 çıkarımlı (`feature/old-search`) şerit görünüyor
- [ ] Dallar yatay şeritler halinde, eskiden yeniye soldan sağa diziliyor; açılışta en güncel kısım görünüyor
- [ ] Fork, merge ve cherry-pick geçişleri farklı renklerde; lejant mevcut; iki renklendirme modu çalışıyor
- [ ] `HEAD` ve `v1.0` tag'i doğru istasyonda işaretli
- [ ] Kenar çubuğunda ahead/behind, merged ve bayat dal bilgisi doğru
- [ ] Commit'e tıklayınca detay paneli; hover'da tooltip
- [ ] `cargo test --workspace` yeşil (core birim + fixture snapshot testleri)
- [ ] Faz 0–6 dokümanları yazılmış

---

## 11. Riskler ve Açık Konular

| Konu | Not / önerilen yaklaşım |
|---|---|
| `gix` API'sinin değişkenliği | gitoxide hâlâ 1.0 öncesi; sürümler arası kırıcı değişiklik olabiliyor. Faz 0'da sürüm sabitlenecek, `gix` kullanımı tamamen `easy-git-repo` crate'i içinde tutulacak — değişiklik olursa etkisi tek crate'le sınırlı. |
| Şerit çıkarımının "yanlış" görünmesi | Algoritma git'in bilmediği bir şeyi tahmin ediyor; bazı geçmişlerde (ör. `develop`'un `main`'e merge edilip sonra `main`'in `develop`'a geri merge edilmesi) sonuç kullanıcının zihnindekinden farklı olabilir. Öncelik listesinin ayarlanabilir olması bu yüzden. Tuhaf vakalar Faz 2 dokümanının "Takıldığın yerler" bölümüne. |
| Fast-forward merge görünmezliği | Teknik olarak çözülemez (bilgi yok). Reflog'dan kısmi çıkarım Faz 10'da değerlendirilebilir; MVP'de dokümanla açıklanır. |
| SVG performansı | 1.500 commit ≈ birkaç bin SVG elemanı; sorunsuz olmalı. Faz 8'de sanallaştırma, gerekirse Canvas. |
| Windows'ta derleme süresi | `gix` + Tauri ilk derlemede birkaç dakika sürebilir. `gix` feature'ları kısılacak; dev profilinde bağımlılıklar için `opt-level = 1`. |
| Kapsam kayması | Metro metaforu ve animasyonlar cazip; MVP'den önce Faz 7–8'e atlanmaması öneriliyor. Salt okunur kararı da bu yüzden net tutuluyor. |

---

## 12. Sonraki Adım

Plan onaylanınca **Faz 0 — `docs/tutorial/00-kurulum-ve-iskelet.md`**: araç zincirinin kurulumu, Tauri + Svelte iskeletinin oluşturulması, Cargo workspace'e iki crate'in eklenmesi ve ilk boş pencerenin açılması.
