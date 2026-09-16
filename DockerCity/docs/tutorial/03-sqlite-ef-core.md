# Faz 3 — SQLite Kalıcılık Katmanı

> **Seri:** DockerCity — docker-compose topoloji dashboard'u
> **Önceki bölüm:** `02-domain-modeli.md` · **Sonraki bölüm:** `04-canvas-ve-mvvm.md`
> **Tahmini süre:** 2–3 saat
> **Kod reposu:** `C:\Users\burak\Development\coding-vibes\DockerCity`

---

## 1. Bu bölümde ne yapacağız

Faz 2'de `InMemoryServiceCategoryResolver` içinde sabit bir tablo bırakmıştık. Yeni bir image eklemek için kodu değiştirip yeniden derlemek gerekiyordu. Bu fazda o tabloyu veritabanına taşıyoruz.

Ama asıl mesele tabloyu taşımak değil — **Faz 2'de ayırdığımız arayüzün karşılığını almak.** `ServiceFactory`'ye tek satır dokunmadan kaynağı değiştireceğiz.

Sonunda elimizde EF Core + SQLite üzerine kurulu bir `DockerCity.Data` katmanı, dört tablo, tohumlanmış 24 eşleme kuralı, üç depo (store) ve 15 yeni test olacak.

---

## 2. Neden böyle

### 2.1 Arayüz bir vaatti, şimdi ödüyoruz

Faz 2'de şunu yazmıştık:

```csharp
public sealed class ServiceFactory(IServiceCategoryResolver resolver) { ... }
```

O gün `IServiceCategoryResolver`'ın tek bir uygulaması vardı ve fazladan bir soyutlama gibi görünüyordu. Bugün ikinci uygulama geliyor ve `ServiceFactory` hiç değişmiyor. Çağıran taraf kurucuya farklı bir nesne veriyor, hepsi bu.

**Soyutlamanın değeri, ikinci uygulama geldiğinde ödenen faturada ölçülür.** Tek uygulaması olan bir arayüz ya gereksizdir ya da henüz vadesi gelmemiştir; hangisi olduğunu ayırt etmek deneyim işidir.

### 2.2 Katalog ile çözücüyü ayırmak

Doğrudan `SqliteServiceCategoryResolver` yazabilirdik. Yazmadık, çünkü kategori tek ihtiyacımız değil: Faz 4'te aynı satırdan **ikon dosyası adı** ve **görünen ad** da lazım olacak.

Bu yüzden araya bir katman daha koyuyoruz:

```
IImageCatalog          kuralları nereden geldiğini bilir
   ├── InMemoryImageCatalog     kod içine gömülü varsayılanlar
   └── SqliteImageCatalog       veritabanından okunanlar

ImageCatalog (abstract)    kuralların nasıl eşleştiğini bilir

CatalogServiceCategoryResolver : IServiceCategoryResolver
                           katalogu ServiceFactory'nin beklediği şekle uydurur
```

Eşleştirme mantığı `ImageCatalog` tabanında tek yerde duruyor. İki kaynak farklı sonuç veremez — testlerden biri tam olarak bunu doğruluyor.

### 2.3 Tohum verisi nereden gelir?

Klasik hata: seed verisini `OnModelCreating` içine elle yazmak, sonra aynı listeyi kod tarafında da tutmak. İki liste zamanla ayrışır.

Bizde `InMemoryImageCatalog.Defaults` **tek kaynak**. Veritabanı ondan tohumlanıyor:

```csharp
private static IEnumerable<ImageMappingEntity> SeedRows() =>
    InMemoryImageCatalog.Defaults.Select((mapping, index) => new ImageMappingEntity
    {
        Id = index + 1,
        ...
    });
```

Bunun bir bedeli var ve bilerek kabul ediyoruz: **`Id` konumsal.** Listenin ortasına satır eklersen sonraki bütün Id'ler kayar ve migration gereksiz yere büyür. Kural basit: yeni kural **sona eklenir**, mevcutlar yeniden sıralanmaz.

### 2.4 `MatchMode` ve `Priority` neden var?

Faz 2'deki eşleştirme `repository == pattern` kadar basitti. Gerçek compose dosyalarında bu yetmiyor:

```
postgres                      → resmi imaj
timescale/timescaledb-postgres → PostgreSQL tabanlı, farklı üretici
bitnami/postgresql            → aynı motor, başka paketleyici
```

Üçü de bir veritabanı ama sadece ilki tam eşleşiyor. Çözüm iki eksenli:

| Eksen | İş |
|---|---|
| `MatchMode` | Kuralın **ne kadarını** kapsadığı — `Exact`, `StartsWith`, `Contains` |
| `Priority` | Birden fazla kural eşleşince **hangisinin kazandığı** |

Tabloda tam eşleşmeler `Priority = 100`, aile yedekleri `Priority = 10`. Böylece `postgres` resmi imajı "PostgreSQL" olarak, `timescale/timescaledb-postgres` ise yine veritabanı olarak ama aile kuralı üzerinden tanınır.

Eşitlik durumunda **uzun desen kazanır** — `redis` ile `redis-stack` çakıştığında daha özgül olanı seçmek için.

### 2.5 Veritabanı nereye yazılır?

`%LOCALAPPDATA%\DockerCity\dockercity.db`

Faz 0'da unpackaged tercihinin gerekçelerinden biri buydu: dosya Explorer'dan görebileceğin normal bir yolda duruyor. Packaged olsaydı izole paket depolamasına düşerdi ve geliştirme sırasında "veritabanı nerede?" diye aramak zorunda kalırdın.

---

## 3. Adım adım uygulama

### Adım 1 — Paketler

`DockerCity.Data` projesine:

```powershell
cd C:\Users\burak\Development\coding-vibes\DockerCity
dotnet add src\DockerCity.Data package Microsoft.EntityFrameworkCore.Sqlite
dotnet add src\DockerCity.Data package Microsoft.EntityFrameworkCore.Design
```

Yazıldığı tarihte güncel sürüm **10.0.12**. `Design` paketini `PrivateAssets="all"` ile işaretle — o yalnızca `dotnet ef` aracının işine yarar, çalışma zamanında yükü taşımanın anlamı yok.

`dotnet ef` aracı kurulu değilse:

```powershell
dotnet tool install --global dotnet-ef
```

---

### Adım 2 — Katalog (Domain)

`src/DockerCity.Domain/Catalog/` altında beş dosya. Özü `ImageMapping`:

```csharp
public sealed record ImageMapping(
    string Pattern,
    MatchMode MatchMode,
    ServiceCategory Category,
    string DisplayName,
    string IconFileName,
    int Priority = 0)
{
    public bool Matches(ImageRef image) => MatchMode switch
    {
        MatchMode.Exact      => image.Repository.Equals(Pattern, StringComparison.OrdinalIgnoreCase),
        MatchMode.StartsWith => image.Repository.StartsWith(Pattern, StringComparison.OrdinalIgnoreCase),
        MatchMode.Contains   => image.Repository.Contains(Pattern, StringComparison.OrdinalIgnoreCase),
        _ => false
    };
}
```

Kazananı seçme işi tabanda:

```csharp
public abstract class ImageCatalog : IImageCatalog
{
    public abstract IReadOnlyList<ImageMapping> All { get; }

    public ImageMapping? Match(ImageRef image) =>
        All.Where(mapping => mapping.Matches(image))
           .OrderByDescending(mapping => mapping.Priority)
           .ThenByDescending(mapping => mapping.Pattern.Length)
           .FirstOrDefault();
}
```

Dikkat: `Matches` **`ImageRef.Repository`** üzerinde çalışıyor, ham metin üzerinde değil. Yani `quay.io/keycloak/keycloak:latest` geldiğinde registry ve tag çoktan ayrılmış durumda, desenin `keycloak/keycloak` olması yeterli. Faz 2'de `ImageRef`'e yatırım yapmamızın karşılığı burada çıkıyor.

`InMemoryServiceCategoryResolver` artık ince bir kabuk:

```csharp
public sealed class InMemoryServiceCategoryResolver()
    : CatalogServiceCategoryResolver(new InMemoryImageCatalog());
```

Faz 2 testlerinin hiçbiri değişmedi — sınıf adı ve davranışı aynı kaldı.

---

### Adım 3 — Varlıklar ve DbContext (Data)

Dört tablo:

| Tablo | Ne tutar | Hangi fazda kullanılacak |
|---|---|---|
| `ImageMappings` | image deseni → kategori, ikon, görünen ad | Faz 3–4 |
| `ComposeProjects` | açılmış dosyalar, son açılış zamanı | Faz 6, 9 |
| `ServiceLayouts` | figür konumları | Faz 6 |
| `AppSettings` | anahtar/değer ayarlar | Faz 8, 9 |

`OnModelCreating` içindeki iki kısıt önemli:

```csharp
entity.HasIndex(mapping => new { mapping.Pattern, mapping.MatchMode }).IsUnique();
```

Aynı deseni aynı modda iki kez tanımlamak anlamsız — veritabanı bunu reddetsin.

```csharp
entity.HasIndex(layout => new { layout.ComposeProjectId, layout.ServiceName }).IsUnique();
```

Bir servisin bir projede tek konumu olur. Bu kısıt olmadan `SaveAsync` sessizce her çağrıda yeni satır ekleyebilirdi ve hatayı ancak figürler üst üste binince fark ederdin.

---

### Adım 4 — Migration

> ⚠️ **Bu adımı senin çalıştırman gerekiyor.** Migration dosyalarını elle yazmak teknik olarak mümkün ama `ModelSnapshot` dosyası modelle birebir eşleşmezse **bir sonraki** migration sessizce yanlış üretilir. Aracın üretmesi tek doğru yol.

```powershell
dotnet ef migrations add InitialCreate --project src\DockerCity.Data --startup-project src\DockerCity.Data
```

`DockerCity.Data` bir sınıf kütüphanesi, yani çalıştırılabilir bir başlangıç projesi yok. `dotnet ef` böyle durumlarda `IDesignTimeDbContextFactory<T>` arar — `DockerCityDbContextFactory` tam olarak bunun için var:

```csharp
public sealed class DockerCityDbContextFactory : IDesignTimeDbContextFactory<DockerCityDbContext>
{
    public DockerCityDbContext CreateDbContext(string[] args) =>
        Create(DockerCityPaths.ConnectionString);
    ...
}
```

Komut `src/DockerCity.Data/Migrations/` altına üç dosya bırakır: migration, tasarımcı dosyası ve model anlık görüntüsü. Üçü de commit edilir.

---

### Adım 5 — Veritabanını açmak

```csharp
public static async Task<DockerCityDbContext> OpenAsync(CancellationToken cancellationToken = default)
{
    Directory.CreateDirectory(DockerCityPaths.DataDirectory);

    var context = DockerCityDbContextFactory.Create(DockerCityPaths.ConnectionString);
    await context.Database.MigrateAsync(cancellationToken);

    return context;
}
```

`MigrateAsync` ile `EnsureCreatedAsync` arasındaki fark bu projede önemli:

| | `EnsureCreated` | `Migrate` |
|---|---|---|
| Şemayı nereden kurar | Doğrudan modelden | Migration dosyalarından |
| Mevcut veritabanını günceller mi | **Hayır** | Evet, eksik migration'ları uygular |
| Migration geçmişi tutar mı | Hayır | Evet |

Faz 9'da ikon yönetim ekranı gelince şema değişecek. `EnsureCreated` kullanıyor olsaydık, kullanıcının mevcut veritabanını silmekten başka yolun kalmazdı.

*(Testlerde yine de `EnsureCreated` kullanıyoruz — orada amaç modelin kendisini sınamak, onu taşıyan migration'ı değil. Her test kendi geçici dosyasıyla çalışır.)*

---

### Adım 6 — Depolar

Üç küçük arayüz, üç uygulama. Hepsi aynı kalıpta; `LayoutStore.SaveAsync` temsili:

```csharp
var existing = await context.ServiceLayouts.SingleOrDefaultAsync(
    layout => layout.ComposeProjectId == composeProjectId
           && layout.ServiceName == serviceName,
    cancellationToken);

if (existing is null)
{
    context.ServiceLayouts.Add(new ServiceLayoutEntity { ... });
}
else
{
    existing.X = position.X;
    existing.Y = position.Y;
    existing.IsPinned = position.IsPinned;
}

await context.SaveChangesAsync(cancellationToken);
```

Ekle-veya-güncelle. Faz 6'da her sürükleme bırakıldığında bu çağrılacak, yani sık çağrılan bir yol — bu yüzden `SingleOrDefault` üzerindeki benzersiz indeks hem doğruluk hem hız için orada.

Arayüzler `DockerCity.Data` içinde yaşıyor, Domain'de değil. Kalıcılık bir domain kavramı değil; Domain'in "bir yerlere kaydedildiğimden" haberi olmasına gerek yok.

---

### Adım 7 — Uygulamada göstermek

Faz 3'ün kendine ait bir arayüzü yok, ama "ilk çalıştırmada veritabanı oluşuyor" iddiasını görmeden geçmeyelim. `MainWindow` açılışta durumu yazıyor:

```csharp
private async Task ShowDatabaseStatusAsync()
{
    try
    {
        using var context = await DatabaseInitializer.OpenAsync();
        var mappings = await context.ImageMappings.CountAsync();

        DatabaseStatusText.Text =
            $"{mappings} image mappings ready · {DockerCityPaths.DatabaseFile}";
    }
    catch (Exception exception)
    {
        DatabaseStatusText.Text = $"Database unavailable: {exception.Message}";
    }
}
```

F5'te pencerede `24 image mappings ready · C:\Users\...\AppData\Local\DockerCity\dockercity.db` görmelisin.

---

### Adım 8 — Çalıştır

```powershell
dotnet build
dotnet test
```

---

## 4. Takıldığın yerler

**`error NETSDK1004: Assets file ... project.assets.json not found` / `Unable to retrieve project metadata`**
`dotnet ef` proje meta verisini okumak için önce derlemeye çalışır, ama restore hiç çalışmamışsa elinde `project.assets.json` olmaz. Solution'ı yeni bir klasöre taşıdıysan ya da `obj/` klasörlerini temizlediysen bu hatayı alırsın:

```powershell
dotnet restore
```

Sonra migration komutunu tekrar çalıştır. Genel kural: `dotnet ef` bir yapı adımı değil, **yapının çıktısını tüketen** bir araç — önce projenin derlenebilir durumda olması gerekir.

**`Unable to create a 'DbContext' of type 'DockerCityDbContext'`**
`dotnet ef` tasarım zamanı fabrikayı bulamamış. `DockerCityDbContextFactory`'nin `public` olduğunu ve `IDesignTimeDbContextFactory<DockerCityDbContext>` uyguladığını doğrula.

**`No project was found` / `Startup project ... is not executable`**
`--startup-project` parametresini de `src\DockerCity.Data` olarak ver. Sınıf kütüphanesinde bu normal.

**`SQLite does not support expressions of type 'DateTimeOffset' in ORDER BY clauses`**
SQLite'ın tarih tipi yoktur. EF Core bir `DateTimeOffset`'i TEXT olarak yazar ve o kolonu `ORDER BY` içinde kullanamaz. `RecentAsync` tam olarak bunu yapıyor.

Üç çözüm var, üçü aynı değil:

| Yaklaşım | Bedeli |
|---|---|
| Sıralamayı istemciye almak (`ToListAsync().OrderByDescending(...)`) | Tabloyu tamamen belleğe çeker; büyüdükçe kötüleşir |
| Alanı `DateTime`'a çevirmek | Sorunu çözer ama "hangi zaman diliminde?" bilgisini modelden atar |
| Değer dönüştürücü ile `long` olarak saklamak | Doğru tip kalır, veritabanı sayı sıralar |

Üçüncüsünü seçiyoruz:

```csharp
entity.Property(project => project.LastOpenedAt)
      .HasConversion(
          value => value.UtcDateTime.Ticks,
          value => new DateTimeOffset(value, TimeSpan.Zero));
```

`UtcDateTime.Ticks` mutlak bir anı tek bir sayıya indirger; sayılar doğru sıralanır ve okurken `DateTimeOffset` geri gelir. Offset bilgisi kayboluyor ama biz zaten hep `UtcNow` yazıyoruz, yani kaybedilen bir şey yok — **bu kararı bilinçli vermek önemli, çünkü yerel saatle yazıyor olsaydın veri kaybı olurdu.**

> Bunu daha önce fark etmemenin yolu yok gibi görünebilir ama var: SQLite'ın tip sistemi (TEXT, INTEGER, REAL, BLOB, NULL) dört tiptir ve tarih yoktur. Sağlayıcıya özgü bu sınırları bilmek, "EF Core her şeyi halleder" beklentisinin nerede biteceğini gösterir.

**`SQLite Error 1: 'no such table: ImageMappings'`**
Migration henüz üretilmemiş ya da commit edilmemiş. Adım 4'e dön.

**Testlerde `The process cannot access the file ... dockercity-xxxx.db`**
SQLite bağlantı havuzu dosyayı tutuyor. `TemporaryDatabase.Dispose` içinde `SqliteConnection.ClearAllPools()` çağırıyoruz, silme yine de başarısız olursa `IOException` yutuluyor — geçici dosya yüzünden test düşmesin.

**Seed'e satır ekledim, migration kocaman çıktı.**
Listenin ortasına eklemişsindir. `Id` konumsal olduğu için sonraki bütün satırlar kaymış olur. Yeni kuralı **sona** ekle.

**`InMemoryServiceCategoryResolver` artık `sealed` ama `CatalogServiceCategoryResolver`'dan türüyor.**
`CatalogServiceCategoryResolver` bilinçli olarak `sealed` değil. Türetilmesi beklenen tek sınıf o.

---

## 5. Ne öğrendik

- **Soyutlamanın faturası ikinci uygulamada kesilir.** Faz 2'de fazladan görünen `IServiceCategoryResolver`, bu fazda `ServiceFactory`'yi hiç açmadan kaynağı değiştirmemizi sağladı.
- **Eşleştirme mantığı ile veri kaynağı farklı sorumluluklardır.** `ImageCatalog` tabanında tek bir `Match` var; bellek ve SQLite kaynakları farklı davranamaz.
- **Tohum verisi tek kaynaktan gelmelidir.** Aynı listeyi iki yerde tutmak, ayrışmayı zaman meselesine çevirir.
- **`HasData` ile Id konumsaldır.** Rahatlık bedava değil; kuralı (sona ekle) bilerek kabul ettik.
- **`Migrate` ile `EnsureCreated` aynı şey değil.** İlki şema evrimini taşır, ikincisi taşımaz. Kullanıcının verisi varsa ikincisi çıkmaz sokaktır.
- **ORM sağlayıcının sınırlarını silmez.** SQLite'ın tarih tipi yok; `DateTimeOffset` ancak bir değer dönüştürücüyle sıralanabilir hale geliyor. Soyutlama, altındaki sistemi bilmekten muaf tutmaz.
- **Kalıcılık arayüzleri Domain'e ait değildir.** Domain nereye kaydedildiğini bilmez; bu bilgi Faz 0'daki bağımlılık yönünün doğal sonucu.

---

## 6. Kendin dene

1. **Kendi image'ını tanıt.** `InMemoryImageCatalog.Defaults` listesinin **sonuna** `hashicorp/vault` için `Identity` kategorisinde bir satır ekle, migration üret, uygulamayı çalıştır. Kaç satırlık bir migration çıktı? Şimdi aynı satırı listenin **başına** ekleyip tekrar dene — farkı gör.

2. **`Priority` gerçekten gerekli mi?** Tablodaki bütün `Priority` değerlerini 0 yap ve testleri çalıştır. Hangi test düşüyor, neden? Uzun desen kuralı tek başına yetiyor mu?

3. **Eksik ikon.** Katalog `postgres.png` diyor ama `assets/icons/` boş. Faz 4'te bu dosyalar bulunamadığında ne olmalı — çökmek mi, varsayılan bir ikon mu, boşluk mu? Kararını şimdi ver ve `ImageMapping`'e bir yorum olarak yaz; Faz 4'te kendi notunu bulacaksın.

4. **İkinci bir compose dosyası aç.** `ComposeProjectStore.OpenAsync`'i iki farklı yol ile çağır, sonra `RecentAsync`'i çalıştır. `ServiceLayouts` tablosunda iki projenin konumları birbirine karışıyor mu? Neden karışmıyor?

5. **Veritabanını sil.** `%LOCALAPPDATA%\DockerCity\dockercity.db` dosyasını sil ve uygulamayı yeniden çalıştır. Ne oluyor? Şimdi aynı şeyi `Migrate` yerine `EnsureCreated` kullanan bir sürümle dene — fark ne zaman ortaya çıkar?

---

**Sonraki bölüm:** `04-canvas-ve-mvvm.md` — artık ekrana bir şey çizme vakti. `CityMap`'i bir `ObservableCollection`'a bağlayıp `Canvas` üzerinde 10 figür göstereceğiz. Orada seni bekleyen ilk tuzak WinUI'nin `Canvas.Left`/`Canvas.Top` attached property'lerinin `ItemsControl` içinde doğrudan bağlanamaması — çözümü `ItemContainerStyle`'dan geçiyor.
