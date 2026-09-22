# Faz 8 — Kullanılabilirlik

> **Seri:** DockerCity — docker-compose topoloji dashboard'u
> **Önceki bölüm:** `07-baglantilar.md` · **Sonraki bölüm:** `09-zoom-pan-animasyon.md`
> **Tahmini süre:** 4–5 saat
> **Kod reposu:** `C:\Users\burak\Development\coding-vibes\DockerCity`

---

## 1. Bu bölümde ne yapacağız

MVP çalışıyor ama bir araç gibi değil, bir demo gibi davranıyor: her açılışta gri bir boşluk, bir düğme, dosyayı yeniden bulmak için dosya seçici.

Bu bölümde uygulamayı **her gün açılacak bir şeye** dönüştürüyoruz:

| Özellik | Nerede |
|---|---|
| Menü çubuğu: File / View / Help | `MainWindow.xaml` |
| Son açılan dosyalar ve temizleme | File › Open recent, karşılama ekranı |
| Dosya dışarıda değişince uyarı, F5 ile yeniden yükleme | Üstte bilgi bandı |
| Karşılama ekranı | Hiç dosya açık değilken |
| Sürükle-bırak ile açma | Pencerenin her yeri |
| Pencere konumunu ve son dosyayı hatırlama | Kapat-aç |
| Açık / koyu / sistem teması | View › Theme |
| Katmanları aç/kapa: detay paneli, yollar, mahalle sınırları | View, Ctrl+1/2/3 |
| PNG olarak dışa aktarma | File › Export, Ctrl+E |
| Klavye kısayolları ve About penceresi | Help, F1 |

Ve veritabanı şeması **ilk kez değişiyor.**

---

## 2. Neden böyle

### 2.1 Plan değişti — ve bu iyi bir şey

Orijinal planda bu faz 9. sıradaydı; önce zoom/pan gelecekti. MVP'yi kullanmaya başlayınca ilk eksik hissedilen şey yakınlaştırma olmadı: **her açılışta aynı dosyayı yeniden aramak** oldu.

Plan bir tahmindir. Kullanım başladığında tahmin veriyle karşılaşır ve kaybeden tahmin olmalı. Faz 8 ile 9 yer değiştirdi, toplam faz sayısı değişmedi.

### 2.2 Son açılanlar için yeni tablo gerekmedi

İlk düşünce "son açılanlar için bir tablo açalım" oldu. Ama Faz 3'te kurduğumuz `ComposeProjects` her açılışta `LastOpenedAt`'i zaten güncelliyordu, `RecentAsync()` o günden beri yazılı ve testliydi. Hiçbir ekran kullanmıyordu, o kadar.

**Yeni bir özellik istediğinde önce var olan veriye bak.** Çoğu zaman eksik olan veri değil, ona bakan ekrandır.

### 2.3 "Temizle" silmemeli

"Clear recent files" komutunun en doğal uygulaması `ComposeProjects` satırlarını silmek. Ama Faz 3'te şu ilişkiyi kurmuştuk:

```csharp
entity.HasMany(project => project.Layouts)
      .WithOne(layout => layout.ComposeProject!)
      .OnDelete(DeleteBehavior.Cascade);
```

Projeyi silmek, kullanıcının elle yerleştirdiği bütün figür konumlarını da siler. Kullanıcı "listeyi temizle" dedi, "emeğimi sil" demedi.

Bu yüzden silmek yerine **gizliyoruz**: `ComposeProjects.IsHiddenFromRecent`. Dosya tekrar açıldığında bayrak kalkıyor ve konumlar yerinde bekliyor. Bunu doğrulayan test:

```csharp
[Fact]
public async Task Clearing_recent_hides_projects_but_keeps_their_layout()
```

### 2.4 Şema ilk kez değişiyor

Faz 3'te şöyle yazmıştık:

> `EnsureCreated` kullanıyor olsaydık, kullanıcının mevcut veritabanını silmekten başka yolun kalmazdı.

O an geldi. `dotnet ef migrations add HideProjectsFromRecent` ikinci bir migration üretiyor ve uygulama açılışta `MigrateAsync` ile onu mevcut veritabanına uyguluyor. Kullanıcının kaydedilmiş konumları, son açılanları, her şeyi yerinde kalıyor.

`EnsureCreated` ile başlasaydık bugün iki seçeneğimiz olurdu: kullanıcıya "veritabanını sil" demek, ya da migration geçmişi olmayan bir veritabanına sonradan migration eklemenin sancılı yolunu yürümek.

#### Gerçekte ne oldu: 137 yeşil test, açılmayan bir uygulama

Bu fazı ilk çalıştırdığımızda bütün testler geçti ama uygulama hiçbir dosyayı açamadı. Sebep: migration komutu çalıştırılmamıştı.

EF Core 9'dan beri `Migrate()`, model ile son migration arasında fark görünce **çalışmayı reddediyor** (`PendingModelChangesWarning`). Veritabanı açılmadı, `CityWorkspace` hiç başlatılamadı.

Testler bunu neden görmedi? Çünkü Faz 3'ten beri test veritabanlarını `EnsureCreated` ile kuruyoruz ve o, şemayı **doğrudan modelden** üretiyor. Model doğruydu, testler de doğruydu. Kusurlu olan, uygulamanın gerçekte çalıştırdığı migration'lardı ve onlara bakan tek bir test yoktu.

Faz 3'te bu tercihi bilerek yapmıştık: *"testler modeli sınıyor, migration'ı değil."* Bugün bu boşluğun bedeli ödendi, ardından da kapatıldı. `MigrationTests` iki şey yapıyor:

```csharp
Assert.False(context.Database.HasPendingModelChanges(), "The model changed but no migration was added. ...");
```

Bir entity değişip migration eklenmediği anda düşüyor ve hata mesajında çalıştırılacak komutu yazıyor. İkinci test sıfırdan bir veritabanını `MigrateAsync` ile kuruyor ve en yeni kolona dokunuyor.

**Ders:** Bir test paketinin yeşil olması, test edilmeyen yolun da doğru olduğunu söylemez. Uygulamanın üretimde kullandığı yol (`Migrate`) ile testlerin kullandığı yol (`EnsureCreated`) farklıysa, ikisi arasındaki farkı sınayan en az bir test olmalı.

#### Ve bir hata mesajı yutuldu

İşi zorlaştıran ikinci bir şey vardı. Asıl hata ("Database unavailable: … pending changes …") durum çubuğunda bir an görünüp kayboluyordu. Açılışta pencere konumu değişiyor, 600 ms sonra tercih kaydı tetikleniyor, veritabanı olmadığı için o da başarısız oluyor ve durum çubuğuna *"Could not save preferences: The workspace has not been initialised"* yazıp gerçek nedeni eziyordu.

Düzeltme iki satır: başlatma hatası `_initialisationError` alanında saklanıyor ve sonraki her eylem onu gösteriyor; veritabanı hazır değilken tercih yazmaları sessizce atlanıyor. **İkincil bir hatanın mesajı, birincil hatanınkini asla ezmemeli.**

### 2.5 Neden `IsHiddenFromRecent`, `IsInRecent` değil?

İlk akla gelen isim `IsInRecent`, varsayılanı `true`. EF Core'da bu bir tuzak:

```csharp
entity.Property(p => p.IsInRecent).HasDefaultValue(true);   // ← tuzak
```

EF, bir `bool` özelliğin değerinin "atanmamış" olup olmadığını CLR varsayılanına bakarak anlar: `false` ise "kullanıcı bir şey demedi" sayar, INSERT'ten çıkarır ve veritabanının varsayılanı olan `true` devreye girer. Yani `IsInRecent = false` yazıp kaydettiğin bir satır veritabanına **`true`** olarak düşer. Sessizce.

EF Core 8+ bunun için `HasSentinel` sunuyor, ama en temiz çözüm adı ters çevirmek: `IsHiddenFromRecent`. CLR varsayılanı `false`, anlamı "gizli değil", veritabanı varsayılanına gerek yok. Tuzak, doğru adlandırmayla ortadan kalkıyor.

### 2.6 DbContext'e bir kapı

Bu faza kadar her veritabanı çağrısı bir kullanıcı eyleminin sonunda `await` ediliyordu; iki çağrının çakışması neredeyse imkânsızdı.

Tercihlerle birlikte bu değişti. Bir menüden temayı değiştirmek arka planda bir yazma başlatıyor (`_ = PersistAsync(...)`) ve kimse onu beklemiyor. Tam o anda bir dosya yükleniyorsa aynı `DbContext` üzerinde iki işlem çakışıyor ve EF Core şu hatayı fırlatıyor:

```
A second operation was started on this context instance before a previous operation completed.
```

Çözüm `CityWorkspace`'e tek girişli bir kapı:

```csharp
private readonly SemaphoreSlim _gate = new(1, 1);

private async Task<T> GatedAsync<T>(Func<Task<T>> work, CancellationToken cancellationToken)
{
    await _gate.WaitAsync(cancellationToken);
    try { return await work(); }
    finally { _gate.Release(); }
}
```

Her veritabanı çağrısı bu kapıdan geçiyor. `lock` yerine `SemaphoreSlim` çünkü `lock` bloğunun içinde `await` kullanılamaz.

### 2.7 Menüdeki kısayollar neden menüde değil?

WinUI'de bir `MenuFlyoutItem`'a doğrudan `KeyboardAccelerator` eklenebilir. Ama flyout içindeki öğelerin kısayolları, menü hiç açılmamışken her zaman güvenilir biçimde çalışmıyor.

Bu yüzden kısayollar kök `Grid`'e bağlandı; menü öğeleri sadece **metni** gösteriyor:

```xml
<Grid.KeyboardAccelerators>
    <KeyboardAccelerator Modifiers="Control" Key="O" Invoked="OnOpenAccelerator" />
    ...
</Grid.KeyboardAccelerators>

<MenuFlyoutItem Text="Open..." KeyboardAcceleratorTextOverride="Ctrl+O" Click="OnOpenClick" />
```

`KeyboardAcceleratorTextOverride` menüde "Ctrl+O" yazısını gösteriyor ama kısayolun kendisi başka yerde.

### 2.8 Menü öğeleri neden binding kullanmıyor?

`ToggleMenuFlyoutItem IsChecked="{Binding ShowLinks, Mode=TwoWay}"` yazmak cazip. Ama flyout'lar görsel ağacın dışında, ayrı bir popup katmanında yaşar; pencerenin `DataContext`'inin oraya nasıl aktığı güvenilir değil.

Bu yüzden menü durumu elle senkronize ediliyor:

```csharp
private void SyncMenuState()
{
    DetailsItem.IsChecked = _viewModel.ShowDetails;
    LinksItem.IsChecked = _viewModel.ShowLinks;
    ...
}
```

ViewModel hâlâ tek doğruluk kaynağı. Kısayolla (Ctrl+2) yapılan değişiklik de `PropertyChanged` üzerinden menüye yansıyor.

### 2.9 Dosya izleyici: içerik mi, olay mı?

`FileSystemWatcher` bir dosyaya dokunulduğunda haber verir, **değiştiğinde** değil. Birçok editör, içerik aynı olsa bile kaydederken dosyaya dokunur. Birçoğu da yerinde yazmaz: geçici bir dosyaya yazıp asıl dosyanın üstüne taşır.

Bu yüzden iki önlem:

1. `Changed`'in yanında `Created` ve `Renamed` de dinleniyor.
2. Olay geldiğinde uyarı hemen gösterilmiyor; dosyanın SHA-256 özeti yüklenen özetle karşılaştırılıyor. Sadece **içerik** farklıysa bant açılıyor.

Ve bir iş parçacığı meselesi: `FileSystemWatcher` olaylarını thread-pool'dan atar. Oradan bir ViewModel özelliğine dokunmak arayüzü çökertir. `DispatcherQueue.TryEnqueue` ile UI iş parçacığına dönülüyor.

Dosyayı okurken de bir incelik var:

```csharp
new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete)
```

`File.OpenRead` yalnızca `FileShare.Read` ister; editör dosyayı yazma için açık tutuyorsa özet alınamaz. `ReadWrite | Delete` paylaşımı, editörle yan yana okumayı mümkün kılıyor.

### 2.10 Pencere konumu: üç tuzak

**Büyütülmüş pencere.** Ekranı kaplayan bir pencere boyutunu "ekran boyutu" olarak bildirir. Onu kaydedip geri yüklersen pencere, büyütülmemiş ama ekran kadar büyük açılır. Bu yüzden normal durumdaki sınırlar ayrı tutuluyor, büyütülmüşlük ayrı bir bayrak olarak saklanıyor.

**Kaybolan monitör.** Pencere dün ikinci monitördeydi; bugün o monitör yok. Konum aynen geri yüklenirse pencere ulaşılamaz bir yerde açılır:

```csharp
if (DisplayArea.GetFromRect(bounds, DisplayAreaFallback.None) is null)
{
    return;
}
```

**Yazma fırtınası.** Başlık çubuğundan sürüklerken pencere saniyede onlarca kez konum değiştirir. Her birinde veritabanına yazmak yerine, pencere 600 ms hareketsiz kalınca bir kez yazılıyor. Klasik "debounce".

`WindowPlacement.TryParse` de bilerek affedici: bozuk ya da elle düzenlenmiş bir değer açılışı çökertmemeli, sessizce varsayılana düşmeli.

### 2.11 PNG dışa aktarma ve şeffaf zemin

`CityCanvas`'ın arka planı yok. Doğrudan onu resme çevirsen şeffaf bir PNG çıkar ve koyu temadaki beyaz yazılar, açık zeminli bir resim görüntüleyicide kaybolur.

Bu yüzden canvas, tema arka planını taşıyan bir `Border`'a sarıldı ve dışa aktarılan o:

```xml
<Border x:Name="CityFrame" Background="{ThemeResource ApplicationPageBackgroundThemeBrush}">
    <controls:CityCanvas x:Name="CityBoard" ... />
</Border>
```

> **Doğrulanması gereken nokta:** `RenderTargetBitmap`'in `ScrollViewer` içinde ekrana sığmayan kısmı da çizip çizmediği. Bunu belgelerden kesin olarak çıkaramadım; ilk denemede büyük bir haritayla kontrol etmek gerekiyor. Çizmiyorsa Faz 9'da zoom geldiğinde "sığdır ve dışa aktar" bir çözüm olur.

### 2.12 Tercihler anında kaydediliyor

"Ayarlar" diye bir pencere ve "Kaydet" düğmesi yok. View menüsünden bir şeyi değiştirdiğinde değişiklik hemen kalıcı oluyor. CommunityToolkit'in ürettiği kısmi metotlar bunu tek satırlık yapıyor:

```csharp
partial void OnShowLinksChanged(bool value) =>
    _ = PersistAsync(preferences => preferences.SetLayerVisibleAsync(AppPreferences.ShowLinksKey, value));
```

Tek bir incelik: açılışta tercihleri okuyup özelliklere atamak da bu metotları tetikler ve okunan değer hemen geri yazılır. `_applyingPreferences` bayrağı bu gereksiz yankıyı kesiyor.

---

## 3. Adım adım uygulama

### Adım 1 — Veri katmanı

- `ComposeProjectEntity.IsHiddenFromRecent`
- `IComposeProjectStore`: `FindAsync`, `ClearRecentAsync`, `RemoveFromRecentAsync`; `OpenAsync` bayrağı geri indiriyor
- `Preferences/`: `ThemePreference`, `WindowPlacement`, `AppPreferences`

`ExecuteUpdateAsync` değişiklik izleyicisini atlar; bellekte o satırı tutan bir varlık eski değeri göstermeye devam eder. Bu yüzden hemen ardından:

```csharp
context.ChangeTracker.Clear();
```

### Adım 2 — Migration

```powershell
dotnet ef migrations add HideProjectsFromRecent --project src\DockerCity.Data --startup-project src\DockerCity.Data
```

Üretilen migration'a bak: `AddColumn<bool>(... defaultValue: false)`. EF, var olan satırlar için bir değer seçmek zorunda ve `bool`'un CLR varsayılanını kullanıyor — ki bizim istediğimiz tam olarak bu.

### Adım 3 — `CityWorkspace`

Kapı (`_gate`), son açılanlar metotları, `UsePreferencesAsync` ve yükleme sırasında **önceki ziyaretin özetini okuyup** yenisiyle karşılaştırma:

```csharp
var previous = await projects.FindAsync(path, cancellationToken);
var changed = previous?.FileHash is { } oldHash && oldHash != hash;
```

Sıra önemli: `OpenAsync` özeti ezeceği için önce `FindAsync`.

### Adım 4 — ViewModel

`MainViewModel` yeni durumlar kazandı: `HasCity`, `CurrentPath`, `IsFileChangedOnDisk`, `ShowDetails/Links/Districts`, `Theme`, `ReopenLastFile`, `RecentFiles`.

`IsDetailsPanelVisible => ShowDetails && HasCity` — iki koşulun birleşimi. Bir değer dönüştürücüyle ifade edilemez; türetilmiş bir özellik olarak ViewModel'de duruyor.

### Adım 5 — Görünüm

- `BoolToVisibilityConverter` — Faz 4'te dönüştürücüden kaçınmıştık; artık dört yerde gerekince yazmaya değdi. `invert` parametresi karşılama ekranı için.
- `CityCanvas`'a `ShowLinks` ve `ShowDistricts` bağımlılık özellikleri. Gizlemek görselleri yok etmiyor, sadece `Collapsed` yapıyor; geri açmak anında.
- `ComposeFileWatcher`, `Dialogs`
- `MainWindow.xaml` baştan yazıldı: menü, bilgi bandı, karşılama ekranı, detay paneli.

### Adım 6 — Çalıştır

```powershell
dotnet test
```

Sonra App'i **derleyip** çalıştır ve Bölüm 1'deki tabloyu tek tek dene.

---

## 4. Takıldığın yerler

**`Database unavailable: The model for context 'DockerCityDbContext' has pending changes`** — ya da durum çubuğunda sadece *"The workspace has not been initialised"*
Migration üretilmemiş. Adım 2'yi çalıştır. `MigrationTests.The_model_has_no_changes_missing_from_a_migration` testi bu durumda düşer; `dotnet test` çıktısında görünür. Bkz. 2.4.

**`SQLite Error 1: 'no such column: c.IsHiddenFromRecent'`**
Migration üretilmiş ama derlenmemiş ya da eski bir exe çalışıyor. Derleyip tekrar dene.

**`The Entity Framework tools version '10.0.3' is older than that of the runtime`**
Zararsız bir uyarı. `dotnet tool update --global dotnet-ef` ile kapanır.

**Menüde Ctrl+O yazıyor ama kısayol çalışmıyor.**
`KeyboardAcceleratorTextOverride` sadece metin. Kısayolun kendisi `RootGrid.KeyboardAccelerators` içinde olmalı.

**`ContentDialog.ShowAsync` "XamlRoot" ile ilgili hata veriyor.**
WinUI masaüstünde her `ContentDialog`'a `XamlRoot` verilmeli: `dialog.XamlRoot = Content.XamlRoot`.

**İki kez F1'e basınca uygulama çöküyor.**
Aynı anda yalnızca bir `ContentDialog` açık olabilir. `_dialogOpen` bayrağı bunun için.

**Dosya değişti bandı hiç açılmıyor.**
Editörün kaydetme biçimine bak. Geçici dosya + taşıma yapıyorsa `Renamed` dinlenmeli. Ayrıca içerik gerçekten değişti mi? Sadece kaydet'e basmak özeti değiştirmez.

**Bant sürekli açılıyor, dosyaya dokunmadığım halde.**
Özet karşılaştırması atlanmış olabilir; olay tek başına yeterli değil.

**Sürükle-bırak çalışmıyor.**
Uygulama yönetici olarak çalışıyorsa (Visual Studio yönetici olarak açıldıysa) Windows, yetkisiz Explorer'dan yetkili bir pencereye sürüklemeyi engeller. Visual Studio'yu normal kullanıcı olarak aç.

**Pencere hep aynı küçük boyutta açılıyor.**
Kaydedilen değer `WindowPlacement.TryParse`'ın alt sınırlarının (640×480) altındaysa yok sayılır. Ya da hiç kaydedilmemiştir: pencereyi taşıdıktan sonra 600 ms beklemeden kapattıysan zamanlayıcı yazmaya fırsat bulamamıştır.

**`A second operation was started on this context instance`**
Bir veritabanı çağrısı kapıyı atlıyor. Bkz. 2.6.

**Dışa aktarılan PNG şeffaf / yazılar görünmüyor.**
`CityFrame` yerine `CityBoard` dışa aktarılıyor.

---

## 5. Ne öğrendik

- **Plan bir tahmindir.** Kullanım başlayınca sıralama değişti ve bu bir başarısızlık değil, planın işini yapması.
- **Önce var olan veriye bak.** Son açılanlar için gereken her şey Faz 3'ten beri tablodaydı.
- **"Temizle" ile "sil" aynı şey değil.** Cascade ilişkisi olan bir tabloda silmek, kullanıcının başka bir emeğini de götürür.
- **Migration bir yatırımdı, faturası bugün ödendi.** Şema değişti, kullanıcının verisi yerinde kaldı.
- **Yeşil test paketi, test edilmeyen yolu kapsamaz.** Testler `EnsureCreated` kullanırken uygulama `Migrate` kullanıyordu; eksik migration 137 yeşil testin arasından geçti. Artık o yolu da sınayan bir test var.
- **İkincil hata birincili ezmemeli.** Asıl neden ekranda bir an görünüp kayboluyordu.
- **Adlandırma bir tuzağı ortadan kaldırabilir.** `IsHiddenFromRecent`, EF Core'un `bool` sentinel tuzağına hiç girmiyor.
- **Beklenmeyen `async` çağrılar çakışır.** `DbContext` iş parçacığı güvenli değil; ateşle-unut yazmalar bir kapıdan geçmeli.
- **Dosya sistemi olayları niyet bildirmez.** Dokunuldu demek değişti demek değil; içeriği karşılaştır.
- **Pencere konumu üç kez yanıltır:** büyütülmüşken, monitör kaybolmuşken ve sürüklenirken.
- **Flyout'lar görsel ağacın dışındadır.** Bağlama yerine elle senkronizasyon, ya da ViewModel'e sadık kalan küçük bir köprü.

---

## 6. Kendin dene

1. **Sentinel tuzağına düş.** `IsHiddenFromRecent` yerine `IsInRecent` + `HasDefaultValue(true)` ile bir dal aç. "Clear recent" testini çalıştır. Ne oluyor? EF Core'un uyarı loglarını aç (`LogTo(Console.WriteLine)`) — seni uyarıyor mu?

2. **Kapıyı kaldır.** `GatedAsync`'i atlayıp doğrudan store çağır. View menüsünde hızla birkaç şeyi aç-kapat ve aynı anda bir dosya yükle. Hatayı yakala. Sonra bu hatayı üretimde bir kullanıcı sana nasıl bildirirdi, düşün.

3. **Farklı editörler.** Compose dosyasını Notepad, VS Code ve Visual Studio ile ayrı ayrı değiştirip kaydet. Hangileri `Changed`, hangileri `Renamed` tetikliyor? `ComposeFileWatcher`'a geçici bir `Debug.WriteLine` ekleyerek gör.

4. **Monitörü "kaldır".** `AppSettings` tablosundaki `window.placement` değerini elle `-5000,-5000,1200,800,0` yap ve uygulamayı aç. Nerede açıldı? `DisplayArea` kontrolünü kaldırıp tekrar dene.

5. **Büyük harita dışa aktarma.** 40 servisli bir compose dosyası oluştur, pencereyi küçült ve Ctrl+E. PNG'de haritanın tamamı var mı? Yoksa ne yapardın?

6. **Kendi menü öğeni ekle.** "File › Open containing folder" — açık dosyanın klasörünü Explorer'da açsın. Hangi API'yi kullanırdın? Klavye kısayolunu nereye bağlarsın?

---

**Sonraki bölüm:** `09-zoom-pan-animasyon.md` — ertelenen yakınlaştırma nihayet geliyor. Oradaki asıl zorluk: Faz 6'da sürükleme koordinatlarını `GetCurrentPoint(this)` ile canvas'a göre alıyorduk. Canvas ölçeklendiğinde o koordinat artık dünya koordinatı değil, ve her şeyin yeniden hesaplanması gerekiyor.
