# DockerCity — Proje Plan Dokümanı

> **Atölye:** Fikirler Atölyesi
> **Doküman sürümü:** v1.1 — 16 Eylül 2026
> **Dokümanlar:** bu klasör (`DockerCity/docs`)
> **Kod:** `C:\Users\burak\Development\coding-vibes\DockerCity`
> **Durum:** Onaylandı — Faz 0 başlıyor

---

## 1. Tek Cümlelik Tanım

Bir `docker-compose.yml` dosyasını okuyup, içindeki servisleri ve aralarındaki ilişkileri **oyunlaştırılmış bir şehir haritası** olarak gösteren Windows masaüstü uygulaması.

## 2. Atölyenin Amacı

Bu proje iki katmanlı bir hedefe hizmet ediyor:

| Katman | Hedef |
|---|---|
| **Ürün** | Çalışan, gerçekten kullanılabilir bir compose görselleştirme aracı |
| **Öğreti** | WinUI 3 / Windows App SDK, YAML işleme, OOP modelleme, EF Core + SQLite, MVVM, Canvas tabanlı çizim ve etkileşim konularında adım adım ilerleyen bir tutorial serisi |

Öncelik **öğreti** tarafında. Her faz; "ne yapacağız → neden böyle → kod → ne öğrendik" akışında ayrı bir doküman olarak üretilecek.

> **Not:** Plan ve öğreti dokümanları MVP sonrası kodun yanına taşındı; ikisi de `coding-vibes/DockerCity` altında. Daha önce ayrı bir repoda tutuluyorlardı.
>
> **Not — repo bağlamı:** `coding-vibes`, AI destekli kodlama denemelerinin tutulduğu repo. Her üst klasör bir çalışma; DockerCity de `.context/`, `README.md`, `src/` konvansiyonunu izliyor. Repo kökündeki `.gitignore` build çıktılarını zaten dışarıda tutuyor.

---

## 3. Ürün Konsepti: Metafor Sözlüğü

Dashboard klasik bir node-graph değil; bir **şehir manzarası**. Compose kavramlarının karşılıkları:

| Compose kavramı | Şehirdeki karşılığı | Görsel ifade |
|---|---|---|
| `docker-compose.yml` | **Şehir** | Tuvalin tamamı |
| `service` | Şehir sakini / aksiyon figür | PNG sprite, zemine yerleşmiş karakter |
| `image` | Karakterin "türü" | Image → ikon eşlemesi (SQLite'ta tutulur) |
| `networks` | **Mahalle** sınırı | Üyelerini çevreleyen yumuşak köşeli bölge |
| *network belirtilmemiş servis* | **Kenar mahalle** (örtük `default` network) | Ayrı renkte, kesikli çizgili bölge |
| `depends_on` | İki figür arasındaki **yol** | Yönlü ok / patika |
| `ports` | Mahallenin **kapısı** | Figürün altında port rozeti |
| `volumes` | **Ambar / depo** | Figürün yanında küçük depo ikonu, isim rozetiyle |
| `environment` | Karakterin **envanteri** | Sadece detay balonunda, sayı rozeti olarak |
| `command` | Karakterin **mesleği/görevi** | Detay balonunda |
| `restart: always` | **Nöbetçi** rozeti | Figür üstünde küçük nişan |

**Etkileşim:** Figürün üzerine gelince balon pencere açılır — servis adı, image, container adı, portlar, volume'ler, bağlı olduğu network'ler, `depends_on` listesi.

### 3.1 Örnek compose dosyasından çıkan kenar durumlar

`samples/docker-compose.yml` bu iş için iyi bir test seti; planlarken hepsini karşılamamız gerekiyor:

1. **`keycloak` ve `minio` hiçbir network'e bağlı değil.** Compose semantiğinde bunlar örtük `default` network'e girer. Uygulamanın bu örtük network'ü modellemesi gerekiyor — aksi halde iki figür havada kalır. *(Şehirde "kenar mahalle" olarak resmedilecek.)*
2. **`qdrant` servisinde `container_name` yok.** Compose bunu `<proje>-<servis>-<index>` kalıbıyla türetir. Uygulama "belirtilmemiş" durumunu ayrı ele almalı.
3. **`ports` iki farklı biçimde:** tekil eşleme (`"5432:5432"`) ve aralık eşlemesi (`"21000-21010:21000-21010"`). Parser her ikisini de anlamalı.
4. **`environment` iki farklı biçimde:** map (`POSTGRES_USER: johndoe`) ve liste (`- RABBITMQ_DEFAULT_USER=guest`). YAML tarafında özel converter gerekir.
5. **`command` iki farklı biçimde:** dizi (`["start-dev"]`) ve düz string (`server --console-address ":9001" /data`).
6. **Volume'ler hem servis içinde hem üst seviyede tanımlı.** Named volume ile bind mount ayrımı yapılmalı.
7. **`qdrant`'ta `restart: always` var**, diğerlerinde yok — opsiyonel alan yönetimi.

---

## 4. Teknoloji Yığını

| Alan | Seçim | Not |
|---|---|---|
| Platform | .NET 10 | Kurulu |
| UI | **WinUI 3 / Windows App SDK** | Fluent görünüm, modern kontroller |
| Dil | C# | `nullable enable`, `ImplicitUsings` açık |
| YAML | **YamlDotNet** | Compose dosyasını DTO'ya çevirmek için |
| Veri | **SQLite** + **EF Core** (`Microsoft.EntityFrameworkCore.Sqlite`) | Code-First + seed |
| MVVM | **CommunityToolkit.Mvvm** | `ObservableObject`, `RelayCommand`, source generator'lar |
| Çizim | Faz 1–6: **XAML `Canvas` + `ItemsControl`/`ItemsRepeater`**<br>Faz 7+: gerekirse **Microsoft.Graphics.Win2D** | Önce binding'li XAML yaklaşımı — öğretici ve MVVM dostu. Ağır zemin/tile çizimi gerekirse Win2D devreye girer. |
| Test | xUnit | Özellikle parser ve domain katmanı için |

### 4.1 Faz 0'da karara bağlanacak teknik detaylar

- **Packaged (MSIX) vs Unpackaged deployment.** Öneri: **unpackaged**. Uygulama düz bir `.exe` olarak çalışır, SQLite dosyası normal `LocalAppData` yoluna düşer, sertifika derdi olmaz. Visual Studio'nun packaged şablonundan başlayıp `csproj`'a `<WindowsPackageType>None</WindowsPackageType>` eklenerek geçilir. Sorun çıkarsa packaged'a dönmek tek satır — Faz 0'da denenip karara bağlanacak.
- **Windows App SDK sürümü.** .NET 10'u destekleyen güncel sürüm kullanılacak; App projesinde `TargetFramework` = `net10.0-windows10.0.19041.0` formatında sabitlenecek. *(Kesin sürüm numaraları Faz 0'da kurulum sırasında doğrulanacak — bugünden çivilenmiyor.)*

---

## 5. Mimari

### 5.1 Solution yapısı

```
coding-vibes/DockerCity/
├── DockerCity.slnx
├── Directory.Build.props
├── src/
│   ├── DockerCity.Domain/         # Entity'ler, abstract base, şehir haritası — UI bağımsız (net10.0)
│   ├── DockerCity.Parsing/        # YamlDotNet DTO'ları + Domain'e mapper        (net10.0)
│   ├── DockerCity.Data/           # EF Core DbContext, SQLite, migration, seed   (net10.0)
│   └── DockerCity.App/            # WinUI 3 uygulaması (View + ViewModel)        (net10.0-windows…)
├── tests/
│   ├── DockerCity.Domain.Tests/
│   └── DockerCity.Parsing.Tests/
├── assets/
│   └── icons/                     # Servis sprite'ları (PNG)
└── tests/
    ├── DockerCity.Domain.Tests/
    └── DockerCity.Parsing.Tests/
```

Dokümanlar `docs/` altında; örnek compose dosyası test fixture'ı olarak `tests/DockerCity.Parsing.Tests/Fixtures/docker-compose.yml` içinde duruyor.

**Bağımlılık yönü:** `App → Data → Domain` ve `App → Parsing → Domain`. Domain hiçbir şeye bağımlı değil.

Domain, Parsing ve Data projeleri **`net10.0`** (platformdan bağımsız) hedefler; sadece App projesi Windows'a bağlıdır. Bu bilinçli bir kısıt: iş mantığının UI'dan bağımsız olduğunu derleyici seviyesinde garantiler, testleri hızlandırır ve ileride bir web arayüzü ya da CLI eklemek istersen çekirdek katman olduğu gibi taşınır.

### 5.2 Domain modeli (OOP omurgası)

Abstract base yaklaşımını iki eksende kuruyoruz:

**Eksen 1 — Compose elemanı olma ortak paydası:**

```
abstract ComposeElement
    ├── string Name
    ├── string? RawKey            // yml'deki anahtar
    └── abstract string Describe()

    ComposeService     : ComposeElement
    ComposeNetwork     : ComposeElement
    ComposeVolume      : ComposeElement
```

**Eksen 2 — Servisin ortak özellikleri ve türe özgü davranışı:**

```
abstract ComposeService : ComposeElement
    ├── ImageRef Image                      (repository + tag)
    ├── string? ContainerName
    ├── IReadOnlyList<PortMapping>          (Host, Container, Protocol, IsRange)
    ├── IReadOnlyList<EnvVariable>
    ├── IReadOnlyList<VolumeMount>          (Named / Bind ayrımı)
    ├── IReadOnlyList<string> NetworkNames
    ├── IReadOnlyList<string> DependsOn
    ├── CommandSpec? Command
    ├── RestartPolicy Restart
    ├── abstract ServiceCategory Category   { get; }
    └── virtual string DefaultIconKey       { get; }

    ├── DatabaseService     (postgres, qdrant, redis…)
    ├── MessagingService    (rabbitmq, nats)
    ├── StorageService      (minio, ftp-server)
    ├── IdentityService     (keycloak)
    ├── ToolingService      (pgadmin, sonarqube)
    └── GenericService      (tanınmayan image'ler için fallback)
```

**Türü kim belirliyor?** `ServiceFactory` — image adına ve SQLite'taki eşleme tablosuna bakarak doğru alt sınıfı üretir. **Factory Method** desenini pratikte görmek için ideal nokta. Eşleme bulunamazsa `GenericService` döner.

> **Faz 2'de düzeltildi:** `ServiceFactory` Domain'de değil **`DockerCity.Parsing`** altında yaşar — DTO'ları girdi aldığı için. Domain compose diye bir dosya formatından haberdar olmaz. Kategori çözümlemesi Domain'deki `IServiceCategoryResolver` arayüzü üzerinden gelir.

Alt tipler yalnızca bir `Category` döndürseydi bu hiyerarşi gereksiz olurdu. Hakkını vermesi için davranış farkı gerekiyor: `MessagingService` broker portu ile yönetim portunu ayırır, `IDataPersisting` uygulayanlar kalıcı veri tespit eder. **Kalıtım "bu ne?", arayüz "bu ne yapabilir?" sorusunu cevaplar.**

**Değer nesneleri** (`record` olarak): `ImageRef`, `PortMapping`, `EnvVariable`, `VolumeMount`, `CommandSpec`.

### 5.3 Şehir haritası (ilişki grafiği)

Parse sonrası `CityMap` nesnesi üretilir:

- **Düğümler:** servisler
- **Kenarlar (3 tip):**
  - `DependsOn` — yönlü, güçlü ilişki (yol olarak çizilir)
  - ~~`SharedNetwork`~~ — **Faz 2'de çıkarıldı.** Mahalle üyeliği bu bilgiyi zaten taşıyor; ayrıca kenar üretmek aynı gerçeği iki kez modellemek olurdu (8 servislik bir ağ 28 kenar demek).
  - `SharedVolume` — yönsüz, aynı ambarı kullanma ilişkisi (ince kesikli çizgi)
- **Bölgeler:** her network bir `District`. Örtük `default` network de bir district olarak üretilir.

---

## 6. SQLite Veri Modeli

**`ImageIconMappings`** — image deseni → ikon eşlemesi (elle girilir, seed ile gömülür)

| Sütun | Tip | Açıklama |
|---|---|---|
| Id | INTEGER PK | |
| ImagePattern | TEXT | `postgres`, `dpage/pgadmin4`, `quay.io/keycloak/keycloak` |
| MatchMode | INTEGER | Exact / StartsWith / Contains |
| IconFileName | TEXT | `postgres.png` |
| Category | INTEGER | ServiceCategory enum değeri |
| DisplayName | TEXT | "PostgreSQL" |
| Priority | INTEGER | Birden fazla desen eşleşirse hangisi kazanır |

*Not: `MatchMode` + `Priority` ikilisi, `redis:latest` ile `redis/redis-stack` gibi durumları ayırt edebilmek için. İlk bakışta fazla görünebilir ama ikinci compose dosyasını açtığında karşına çıkacak bir ihtiyaç.*

**`ComposeProjects`** — açılmış dosyaların kaydı (Id, Name, FilePath, LastOpenedAt, FileHash)

**`ServiceLayouts`** — figür konumları (Id, ComposeProjectId FK, ServiceName, X, Y, IsPinned)

**`AppSettings`** — anahtar/değer (tema, zoom seviyesi, son açılan proje)

**Seed verisi:** Faz 3'te en az 12 image için eşleme gömülecek — postgres, pgadmin4, rabbitmq, redis, nats, minio, qdrant, keycloak, sonarqube, ftp-server, mongo, nginx.

---

## 7. Görsel Tasarım Kararları

| Konu | Karar |
|---|---|
| Zemin | Düz gri (`#2B2B2B` koyu tema / `#F3F3F3` açık tema). Tile/doku Faz 8'e ertelendi. |
| Mahalle bölgesi | Üyelerin bounding box'ı + padding, 16px yuvarlatılmış köşe, %12 opaklıkta dolgu + belirgin kenarlık. Her network'e paletten bir renk atanır. |
| Örtük default bölge | Kesikli kenarlık, nötr gri, "default (örtük)" etiketi |
| Figür boyutu | 64×64 px sprite + altında servis adı etiketi |
| Balon pencere | `ToolTip` yerine özel `Popup` — 300ms gecikme, imleç takibi, zengin içerik (ikon + tablo) |
| Seçim | Tıklanınca figür etrafında vurgu halkası + sağda detay paneli açılır |
| Bağlantı okları | `depends_on` için Bezier eğrisi, ok başlı, hedefe doğru |
| Z-sırası | Zemin → mahalle bölgeleri → bağlantı okları → figürler → etiketler → popup |

**Renk paleti:** Mahalle bölgeleri için 8 renklik, koyu ve açık temada da ayırt edilebilen bir dizi hazırlanacak (Faz 5).

---

## 8. Yol Haritası

MVP = **Faz 0 → Faz 6**. Sonrası cila ve genişleme.

### Faz 0 — Atölye kurulumu
`docs/tutorial/00-kurulum-ve-iskelet.md`

- Windows App SDK ve gerekli workload'ların kurulumu
- Solution iskeleti (4 proje + 2 test projesi), `Directory.Build.props`
- WinUI 3 App projesi, packaged/unpackaged kararı
- Boş pencere çalışıyor

**Kazanım:** WinUI 3 proje anatomisi, `App.xaml.cs` yaşam döngüsü, `Window` vs `Page`, deployment modelleri arasındaki fark, çok projeli solution'da bağımlılık yönü.
**Çıktı:** Boş pencere açılıyor, `dotnet build` temiz geçiyor.

---

### Faz 1 — YAML'ı okumak
`docs/tutorial/01-yaml-parser.md`

- YamlDotNet ile compose dosyasının DTO'ya deserialize edilmesi
- Bölüm 3.1'deki **7 kenar durumun** tamamının ele alınması
- Polimorfik alanlar için özel `IYamlTypeConverter` yazımı (environment map/list, command string/array)
- xUnit ile parser testleri — örnek dosya fixture olarak

**Kazanım:** Deserialization, esnek şema yönetimi, "gerçek dünya verisi hiç düzgün gelmez" dersi.
**Çıktı:** Testler `samples/docker-compose.yml`'ı okuyup 10 servisi doğru şekilde döküyor.

---

### Faz 2 — Domain modeli ve OOP hiyerarşisi
`docs/tutorial/02-domain-modeli.md`

- `ComposeElement` ve `ComposeService` abstract sınıfları
- Alt sınıflar + `ServiceFactory` (Factory Method)
- Değer nesneleri (`record`)
- DTO → Domain mapper
- `CityMap` üretimi: 3 kenar tipi + örtük default network'ün üretilmesi

**Kazanım:** Kalıtım vs kompozisyon tercihi, abstract sınıf ne zaman anlamlı, Factory Method, `record` ile değer eşitliği, DTO/Domain ayrımının gerekçesi.
**Çıktı:** Parse edilen dosyadan tam bir `CityMap` nesnesi; testlerle doğrulanmış ilişki sayıları.

---

### Faz 3 — SQLite kalıcılık katmanı
`docs/tutorial/03-sqlite-ef-core.md`

- EF Core + SQLite kurulumu, `CityDbContext`
- 4 tablonun Code-First tanımı, migration üretimi
- İkon eşlemelerinin `HasData` ile seed edilmesi
- Repository arayüzleri, veritabanı dosyasının `LocalAppData` altına yerleşmesi

**Kazanım:** EF Core Code-First, migration yaşam döngüsü, seeding, SQLite'ın masaüstü uygulamalarındaki yeri.
**Çıktı:** İlk çalıştırmada veritabanı oluşuyor, `ServiceFactory` eşlemeleri veritabanından okuyor.

---

### Faz 4 — İlk görselleştirme
`docs/tutorial/04-canvas-ve-mvvm.md`

- MVVM iskeleti (CommunityToolkit.Mvvm), `MainViewModel`
- Dosya açma (`FileOpenPicker` + pencere handle'ı) → parse → harita → ViewModel
- `ItemsControl` + `Canvas` ItemsPanel, `Canvas.Left/Top` binding'i
- `ServiceNodeControl` UserControl: sprite + etiket
- Basit otomatik yerleşim: network başına ızgara/daire düzeni

**Kazanım:** MVVM, data binding, `DataTemplate`, attached property binding (WinUI'de küçük bir tuzak — `ItemContainerStyle` üzerinden çözülür), `x:Bind` vs `Binding` farkı.
**Çıktı:** Compose dosyası açılınca 10 figür ekranda görünüyor.

---

### Faz 5 — Mahalle sınırları
`docs/tutorial/05-mahalle-bolgeleri.md`

- Mahalle bölgelerinin bounding box hesabı
- Bölgelerin figürlerin ardına çizimi, z-order yönetimi
- Renk paleti ataması, bölge etiketleri
- Örtük `default` bölgesinin farklı stille gösterimi
- Bir servisin birden fazla network'e üye olma durumu (örtüşen bölgeler)

**Kazanım:** Layout matematiği, `Canvas` üzerinde katman yönetimi, geometri hesabı, `Path`/`Geometry` kullanımı.
**Çıktı:** `fnp-network` mahallesi ile kenar mahalle görsel olarak ayrışıyor.

---

### Faz 6 — Etkileşim *(MVP tamamlanır)*
`docs/tutorial/06-etkilesim.md`

- Hover → detay balonu (özel `Popup`, gecikme, konumlandırma)
- Tıklama → seçim + sağ detay paneli
- Sürükle-bırak (`PointerPressed`/`Moved`/`Released` veya `ManipulationDelta`)
- Konumun `ServiceLayouts` tablosuna kaydı, yeniden açılışta geri yüklenmesi

**Kazanım:** Pointer olay modeli, pointer capture, hit-testing, kalıcı UI durumu.
**Çıktı:** **Çalışan MVP.** Dosya aç, şehri gör, figürleri incele ve yerleştir, kapat, aç — düzen korunuyor.

---

### Faz 7 — Yollar ve ilişkiler
`docs/tutorial/07-baglantilar.md`

`depends_on` oklarının Bezier eğrisiyle çizimi, sürükleme sırasında canlı güncellenmesi, paylaşılan volume bağlantıları, port rozetleri.

**Kazanım:** `PathGeometry`, Bezier kontrol noktası hesabı, çizim performansı.

---

### Faz 8 — Oyunlaştırma cilası
`docs/tutorial/08-zoom-pan-animasyon.md`

Zoom/pan (`ScrollViewer` veya `CompositeTransform`), giriş animasyonları, hover'da figür "zıplaması", `restart: always` için nöbetçi rozeti, mini harita.

**Kazanım:** Transform zinciri, composition animasyonları, viewport yönetimi.

---

### Faz 9 — Kullanılabilirlik ve yönetim
`docs/tutorial/09-kullanilabilirlik.md`

Son açılan dosyalar, hata yönetimi (bozuk YAML), ikon eşleme yönetim ekranı (CRUD), açık/koyu tema, PNG olarak dışa aktarma, MSIX paketleme değerlendirmesi.

---

### Faz 10 — İleri seviye *(opsiyonel)*
`docs/tutorial/10-canli-docker.md`

Docker.DotNet ile canlı bağlantı: hangi container ayakta, sağlık durumu figürlerin üstünde canlı rozet olarak. Şehir "yaşamaya" başlar.

---

## 9. Doküman Düzeni

Her tutorial dosyası aynı kalıpta:

1. **Bu bölümde ne yapacağız** — hedefin bir paragrafı
2. **Neden böyle** — alternatifler ve tercih gerekçesi *(atölyenin asıl değeri burada)*
3. **Adım adım uygulama** — tam kod blokları, dosya yollarıyla
4. **Takıldığın yerler** — bilinen tuzaklar ve çözümleri
5. **Ne öğrendik** — kazanımların özeti
6. **Kendin dene** — bölümü pekiştirecek 2-3 küçük görev

Dosya adlandırma: `NN-konu-adi.md`, `docs/tutorial/` altında.

---

## 10. MVP Kabul Kriterleri

MVP'yi "bitti" saymak için:

- [ ] `samples/docker-compose.yml` hatasız parse ediliyor — 10 servis, 4 volume, 1 açık + 1 örtük network
- [ ] Her servis doğru kategoriye ve ikona eşleşiyor; eşleşmeyen `GenericService` olarak düşüyor
- [ ] `fnp-network` üyeleri bir bölge içinde, `keycloak` ve `minio` ayrı "kenar mahalle" bölgesinde
- [ ] Hover'da balon açılıyor; image, container adı, portlar, volume'ler ve network'ler görünüyor
- [ ] Figürler sürüklenebiliyor, konum kaydediliyor ve yeniden açılışta geri geliyor
- [ ] Parser ve domain katmanı için testler yeşil
- [ ] Faz 0–6 dokümanları yazılmış

---

## 11. Riskler ve Açık Konular

| Konu | Not / önerilen yaklaşım |
|---|---|
| WinUI 3'te `Canvas` + `ItemsControl` attached property binding'i | WPF'e göre daha kısıtlı. `ItemContainerStyle` üzerinden `Canvas.Left/Top` bağlanacak. Faz 4'te doğrulanmalı; sorun çıkarsa `ItemsRepeater` + özel `Layout` alternatifi var. |
| Dosya seçici (WinUI 3 masaüstü) | `FileOpenPicker` pencere handle'ı ister — `WinRT.Interop.InitializeWithWindow`. Packaged/unpackaged fark etmez, her iki durumda da gerekli. Faz 4'ün bilinen tuzağı. |
| Sprite'ların temini | Faz 4'te **placeholder** geometrik ikonlarla başlanacak; gerçek "aksiyon figür" görselleri sonradan `assets/icons/` içine bırakılarak değiştirilebilir. Kod bundan etkilenmez. |
| Performans | 10 servis için sorun yok. 50+ servisli dosyalarda XAML eleman sayısı sorun olursa Win2D'ye geçiş yolu açık bırakıldı. |
| Kapsam kayması | Faz 8 (oyunlaştırma cilası) en cazip ama en az öğretici kısım. MVP bitmeden oraya atlanmaması öneriliyor. |

---

## 12. Sonraki Adım

**Faz 0 — `docs/tutorial/00-kurulum-ve-iskelet.md`** hazır. Windows App SDK kurulumu, solution iskeletinin komut satırından oluşturulması ve ilk boş pencerenin açılması.
