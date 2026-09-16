# Faz 4 — Canvas ve MVVM

> **Seri:** DockerCity — docker-compose topoloji dashboard'u
> **Önceki bölüm:** `03-sqlite-ef-core.md` · **Sonraki bölüm:** `05-mahalle-bolgeleri.md`
> **Tahmini süre:** 3–4 saat
> **Kod reposu:** `C:\Users\burak\Development\coding-vibes\DockerCity`

---

## 1. Bu bölümde ne yapacağız

Üç fazdır ekranda "The city is under construction." yazıyor. Artık şehri kuruyoruz.

Sonunda: bir compose dosyası seçiyorsun, 10 figür ekrana diziliyor, üstte `fnp-network` mahallesinin sekiz sakini, altta kenar mahallenin ikisi. Üzerlerine gelince açıklama balonu çıkıyor.

Mahalle **sınırlarını** henüz çizmiyoruz — o Faz 5. Bu fazda figürlerin doğru yere gelmesi yeterli.

---

## 2. Neden böyle

### 2.1 Önce bir özür: Faz 0'da yanlış söylemiştim

Faz 0'ın kapanışında bu bölümün tuzağı için şunu yazmıştım:

> WinUI'de `Canvas.Left`/`Canvas.Top` binding'i `ItemContainerStyle` üzerinden çözülür.

**Bu WPF bilgisi ve WinUI'de çalışmıyor.** WinUI (ve öncesinde UWP) `Style` setter'larının içinde binding'e izin vermez — `<Setter Value="{Binding X}" />` derlenmez bile. WPF'ten gelen refleksle yazmışım.

Bunu silip geçmek yerine bırakıyorum, çünkü tam da bu serinin konusu: **bir platformdan diğerine taşınan bilgi, taşınmadığını ancak denediğinde söyler.**

### 2.2 Peki gerçekte ne çalışıyor?

Bir koleksiyonu Canvas üzerinde konumlandırmanın WinUI'deki halleri:

| Yol | Sorun |
|---|---|
| `ItemsControl` + `ItemsPanel=Canvas`, `Canvas.Left` DataTemplate kökünde | WinUI her öğeyi bir `ContentPresenter` ile sarar; Canvas'ın çocuğu o presenter'dır. Template kökündeki `Canvas.Left` yanlış elemana yazılır, hiçbir şey olmaz. |
| `ItemContainerStyle` ile presenter'a binding | Style setter'ında binding desteklenmiyor. |
| `ItemsRepeater` + özel `Layout` | Çalışır ve idiomatic'tir, ama sanallaştırma altyapısı 10 figür için ağır bir bedel. |
| Canvas'ı koleksiyonla elle senkron tutmak | Tesisat elle yazılır ama davranışı tamamen öngörülebilir. |

Sonuncusunu seçiyoruz. `CityCanvas` yaklaşık 60 satır ve tek işi var: koleksiyondaki her düğüm için bir görsel tutmak, konumlarını `X`/`Y` değiştikçe güncellemek.

**Burada MVVM'den taviz vermiyoruz** — ViewModel hâlâ tek doğruluk kaynağı. Elle yazdığımız şey veri değil, tesisat. Bu ayrımı korumak önemli: `CityCanvas` hiçbir zaman bir konuma karar vermez, sadece ViewModel'in söylediğini uygular. Faz 6'da sürükleme geldiğinde fare olayı ViewModel'in `X`'ini değiştirecek, canvas da onu izleyecek.

### 2.3 Yerleşim algoritması neden Domain'de?

`GridCityLayoutEngine` `DockerCity.Domain/Layout/` altında. İlk bakışta tuhaf: koordinat hesabı bir sunum işi değil mi?

İki gerekçe:

**Saf matematik, UI tipi yok.** `LayoutPoint(double X, double Y)` bir `Windows.Foundation.Point` değil. Motor hiçbir WinUI tipine dokunmuyor.

**Ve asıl mesele: test edilebilirlik.** Bu fazın kodunun büyük kısmı ancak gözle doğrulanabilir — pencere açılır, figürler görünür ya da görünmez. Yerleşim matematiğini Domain'e koyduğumuzda onu `dotnet test` ile sınayabiliyoruz. Nitekim `CityLayoutTests` içinde beklenen koordinatlar tek tek çivilenmiş durumda:

```csharp
Assert.Equal(new LayoutPoint(64, 64), layout.Nodes["ftp-server"]);
Assert.Equal(new LayoutPoint(496, 64), layout.Nodes["postgres"]);
Assert.Equal(new LayoutPoint(64, 440), layout.Nodes["keycloak"]);
```

Bir gün `NodeSpacing`'i değiştirirsen bu testler bağırır. Gözle bakarak "sanırım biraz kaymış" demekten iyidir.

**Genel kural:** görsel bir özellikte, hesaplanabilen kısmı hesaplanamayan kısımdan ayır ve hesaplanabilir olanı test et.

### 2.4 `CityLayout` neden mahalle sınırlarını da döndürüyor?

Faz 5'te lazım olacak diye. Motor zaten her bloğun nerede başlayıp nerede bittiğini biliyor; o bilgiyi atıp Faz 5'te yeniden hesaplamak hem israf hem de iki hesabın ayrışma riski.

Bugün `DistrictBounds` listesini kimse kullanmıyor ama testleri yazılmış durumda. Faz 5 sadece çizecek.

---

## 3. Adım adım uygulama

### Adım 1 — Paket

```powershell
dotnet add src\DockerCity.App package CommunityToolkit.Mvvm
```

Yazıldığı tarihte **8.4.2**. Bize kaynak üreteçleri lazım: `[ObservableProperty]` ile alanlardan `INotifyPropertyChanged` uygulayan özellikler üretiyor. Elle yazılan `OnPropertyChanged(nameof(...))` satırlarının tamamından kurtarıyor.

> `[ObservableProperty]` kullanan her sınıf `partial` olmalı — üreteç kodu ikinci bir parçaya yazıyor.

---

### Adım 2 — Yerleşim motoru (Domain)

`src/DockerCity.Domain/Layout/` altında beş dosya. Çekirdeği:

```csharp
public CityLayout Arrange(CityMap map, LayoutOptions? options = null)
{
    var settings = options ?? LayoutOptions.Default;

    var nodes = new Dictionary<string, LayoutPoint>(StringComparer.Ordinal);
    var bounds = new List<DistrictBounds>();

    var cursorY = settings.Margin;

    foreach (var district in OrderDistricts(map))
    {
        var members = district.Members
            .Where(member => !nodes.ContainsKey(member.Name))
            .OrderBy(member => member.Name, StringComparer.Ordinal)
            .ToList();
        ...
    }
}
```

Üç karar gizli:

**`!nodes.ContainsKey(member.Name)`** — Faz 2'de not ettiğimiz durum: bir servis birden fazla ağa üye olabilir. Figür ikiye bölünemeyeceği için ilk mahallesinde çiziliyor. Faz 5'te örtüşen bölgeleri çizerken bu karar yine karşımıza çıkacak.

**`OrderBy(... StringComparer.Ordinal)`** — sözlük sırası garanti değil. Sıralamazsan aynı dosya iki açılışta farklı dizilebilir ve "neden figürler yer değiştirdi?" diye saatlerini harcarsın. `Arrangement_is_repeatable` testi bunu koruyor.

**`OrderBy(district => district.IsImplicit)`** — `bool` sıralamasında `false` önce gelir, yani açık mahalleler üstte, örtük `default` en altta. Kenar mahalle zaten "artakalanlar" demek; en altta olması hem mantıklı hem de görsel olarak doğru hikâyeyi anlatıyor.

---

### Adım 3 — ViewModel'ler

`ServiceNodeViewModel` bir `ComposeService`'i sarıyor. Domain nesnesini doğrudan XAML'e bağlamıyoruz, çünkü:

- `X`/`Y` domain'e ait değil, ekrana ait
- `Initials`, `Badges`, `ImageLabel` gibi alanlar sunum kararları

```csharp
public sealed partial class ServiceNodeViewModel : ObservableObject
{
    [ObservableProperty] private double _x;
    [ObservableProperty] private double _y;

    public string Name => _service.Name;
    public ServiceCategory Category => _service.Category;
    public string Badges => string.Join(" · ", BadgeParts());
}
```

`Badges` Faz 2'de eklediğimiz davranışların ilk görünür karşılığı:

```csharp
private IEnumerable<string> BadgeParts()
{
    if (_service.IsSupervised) yield return "restarts";
    if (_service is IDataPersisting { HasPersistentData: true }) yield return "persistent";
    if (_service is IWebAccessible { WebUrl: not null }) yield return "web";
    if (_service.Image.IsLatest) yield return "latest";
}
```

Faz 2'de "kalıtım ne, arayüz ne yapabilir" ayrımını yaparken bunun bir gün işe yarayacağını söylemiştik. İşte yeri: desen eşlemeli `is IDataPersisting { HasPersistentData: true }` ifadesi, tür kontrolü ile özellik kontrolünü tek satırda yapıyor.

`MainViewModel` dosyayı yükleyip koleksiyonu dolduruyor. Dikkat edilecek nokta:

```csharp
var city = await Task.Run(() => _workspace.Load(path));
```

Parse + domain kurulumu + yerleşim hesabı arka plana alınıyor. 10 servis için gerekmez ama 200 servislik bir dosyada UI donar. **Alışkanlığı baştan doğru kurmak sonradan aramaktan ucuz.**

---

### Adım 4 — `CityCanvas`

```csharp
public sealed class CityCanvas : Canvas
{
    private readonly Dictionary<ServiceNodeViewModel, FrameworkElement> _visuals = [];

    public static readonly DependencyProperty NodesProperty = DependencyProperty.Register(
        nameof(Nodes),
        typeof(ObservableCollection<ServiceNodeViewModel>),
        typeof(CityCanvas),
        new PropertyMetadata(null, OnNodesChanged));
    ...
}
```

`Rebuild` her düğüm için bir `ServiceNodeControl` yaratıp `DataContext`'ini atıyor ve `Canvas.SetLeft/SetTop` ile yerleştiriyor. Sonra düğümün `PropertyChanged`'ine abone oluyor:

```csharp
private void OnNodePropertyChanged(object? sender, PropertyChangedEventArgs args)
{
    if (sender is not ServiceNodeViewModel node || !_visuals.TryGetValue(node, out var visual)) return;

    if (args.PropertyName == nameof(ServiceNodeViewModel.X)) SetLeft(visual, node.X);
    else if (args.PropertyName == nameof(ServiceNodeViewModel.Y)) SetTop(visual, node.Y);
}
```

Abonelikten **çıkmayı** unutma — `Rebuild` başında eski düğümlerin aboneliği kaldırılıyor. Olay aboneliği bir referanstır; kaldırmazsan eski ViewModel'ler canvas yaşadığı sürece bellekte kalır. Bu, olay tabanlı kodun en sessiz sızıntısıdır.

---

### Adım 5 — `x:Bind` değil `Binding`

`ServiceNodeControl.xaml` klasik `{Binding}` kullanıyor:

```xml
<TextBlock Text="{Binding Name}" />
```

`x:Bind` daha hızlıdır (derleme zamanında çözülür, yansıma kullanmaz) ama `x:Bind` **sayfanın/denetimin kendi tipine** bağlanır, `DataContext`'e değil. Bizde `DataContext` çalışma zamanında atanıyor, dolayısıyla klasik `Binding` doğru araç.

İki fark daha bilmeye değer:

| | `Binding` | `x:Bind` |
|---|---|---|
| Kaynak | `DataContext` | Sayfanın/denetimin kendisi |
| Varsayılan mod | OneWay | **OneTime** |
| Hata | Çalışma zamanında sessiz | Derleme zamanında |

`x:Bind`'ın varsayılanının OneTime olması en çok zaman kaybettiren ayrıntısıdır — değer bir kez yazılır, sonra güncellenmez ve hiçbir hata almazsın.

---

### Adım 6 — Pencere

`MainWindow.xaml`'da dikkat edilecek tek yapısal şey:

```xml
<Grid x:Name="RootGrid" ...>
```

ve code-behind'da:

```csharp
RootGrid.DataContext = _viewModel;
```

**WinUI'de `Window` bir `FrameworkElement` değildir, dolayısıyla `DataContext`'i yoktur.** WPF'te `this.DataContext = vm` yazardın; burada yazamazsın, derlenmez. ViewModel kök `Grid`'e bağlanır, oradan aşağı miras alınır.

Dosya seçici:

```csharp
var picker = new FileOpenPicker { SuggestedStartLocation = PickerLocationId.ComputerFolder };

InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(this));

picker.FileTypeFilter.Add(".yml");
picker.FileTypeFilter.Add(".yaml");
```

Faz 0'da söz verdiğimiz tuzak buydu. Masaüstü uygulamasında seçicinin üstüne oturacağı bir pencere yok, hangi pencerenin sahibi olduğu elle söylenmeli. `FileTypeFilter` **boş bırakılamaz** — en az bir uzantı eklemezsen `PickSingleFileAsync` çalışma zamanında patlar.

---

### Adım 7 — Çalıştır

```powershell
dotnet restore
dotnet test
```

Sonra F5 → **Open compose file...** → `tests\DockerCity.Parsing.Tests\Fixtures\docker-compose.yml`.

Alt çubukta şunu görmelisin:

```
10 services · 2 districts (1 implicit) · docker-compose.yml
```

---

## 4. Takıldığın yerler

**`The name 'RootGrid' does not exist in the current context`**
XAML'de `x:Name` verdiğin elemanın alanı derleme sırasında üretilir. Ad yanlışsa ya da XAML derlenmediyse çıkar. `obj` klasörünü silip yeniden derle — WinUI'de üretilen kod inatçıdır.

**`Cannot assign to DataContext` / `Window does not contain a definition for DataContext`**
WPF refleksi. Bkz. Adım 6: ViewModel kök panele bağlanır.

**Figürler görünmüyor ama alt çubukta "10 services" yazıyor.**
`CityBoard.Nodes` atanmamış olabilir. Bu bir XAML binding'i değil, code-behind'da elle yapılan bir atama:
```csharp
CityBoard.Nodes = _viewModel.Nodes;
```

**Figürler üst üste tek noktada duruyor.**
`Canvas.SetLeft/SetTop` çağrıları `Children.Add`'den sonra yapılmışsa sorun olmaz, ama görsel `Canvas`'ın çocuğu değilse hiç etkisi olmaz. `ServiceNodeControl`'ün doğrudan `Children`'a eklendiğini doğrula — araya bir `Border` koyarsan `Canvas.Left` o `Border`'a yazılmalı.

**`PickSingleFileAsync` çalışma zamanında hata veriyor.**
`FileTypeFilter` boş. En az bir uzantı ekle.

**Rozetler boş görünüyor.**
`Badges` hiçbir koşul tutmazsa boş metin döner ve `TextBlock` boş bir satır kadar yer kaplar. Fixture'da `redis` böyledir — `restarts` yok, kalıcı verisi yok, web arayüzü yok. Doğru davranış.

**`ProgressRing` hep dönüyor.**
`IsBusy` bir `finally` bloğunda `false`'a çekiliyor mu? Hata yolunda unutulursa sonsuza kadar döner.

---

## 5. Ne öğrendik

- **Platform bilgisi taşınmaz.** WPF'in `ItemContainerStyle` çözümü WinUI'de yok; Style setter'larında binding desteklenmiyor. Bir çerçeveden diğerine geçerken "bu böyle yapılır" cümlesi her zaman sınanmalı.
- **Tesisatı elle yazmak MVVM'i bozmaz.** ViewModel tek doğruluk kaynağı kaldığı sürece, aradaki köprünün elle yazılmış olması bir tasarım kusuru değil, bir maliyet tercihidir.
- **Hesaplanabilir olanı ayır ve test et.** Yerleşim matematiği Domain'de olduğu için `dotnet test` ile doğrulanıyor; geri kalanı gözle bakmak zorundayız.
- **Sıralamayı asla şansa bırakma.** `OrderBy` olmadan aynı dosya iki açılışta farklı dizilir ve sebebini bulmak saat alır.
- **`x:Bind` varsayılanı OneTime'dır.** Sessizce güncellenmeyen bir arayüz çoğu zaman budur.
- **`Window` WinUI'de `FrameworkElement` değildir.** `DataContext`, `Resources`, `Style` — pek çok alışkanlık burada durur.
- **Olay aboneliği bir referanstır.** `+=` yazdığın her yerde `-=` nerede diye sor.

---

## 6. Kendin dene

1. **`x:Bind`'ı dene ve kır.** `ServiceNodeControl`'de bir `TextBlock`'u `{x:Bind ...}` ile bağlamayı dene. Ne oluyor, neden? Sonra `ServiceNodeControl`'e bir `Node` bağımlılık özelliği ekleyip `{x:Bind Node.Name, Mode=OneWay}` ile çalıştır. Hangisi daha çok kod, hangisi daha hızlı?

2. **Sıralamayı boz.** `GridCityLayoutEngine` içindeki iki `OrderBy` çağrısını kaldır, testleri çalıştır. Hangisi düşüyor? Uygulamayı birkaç kez açıp kapat — dizilim değişiyor mu?

3. **Gerçek ikonları koy.** `assets/icons/postgres.png` ekle ve `ServiceNodeControl`'ün bunu `ms-appx:///` üzerinden yüklemesini sağla. Dosya yoksa ne olmalı? (Faz 3'ün "kendin dene" bölümünde bu kararı vermiştin — notunu bul.)

4. **200 servislik dosya.** Fixture'ı çoğaltarak 200 servisli bir compose dosyası üret ve aç. Ne kadar sürüyor? `Task.Run` olmasaydı ne hissederdin? `CityCanvas.Rebuild` 200 kontrol yaratıyor — sorun burada mı, yoksa başka yerde mi?

5. **Rozet görünürlüğü.** Boş `Badges` hâlâ satır yüksekliği kadar yer kaplıyor. Bunu düzeltmek için bir `IValueConverter` mi yazarsın, ViewModel'e `Visibility` mi eklersin, yoksa `x:Load` mı kullanırsın? Üçünün de bedelini bir cümleyle yaz.

---

**Sonraki bölüm:** `05-mahalle-bolgeleri.md` — `CityLayout.Districts` listesi hazır bekliyor; artık onu çizeceğiz. Örtük `default` mahallesi kesikli sınırla, açık ağlar dolu sınırla. Orada asıl mesele z-sırası olacak: bölgeler figürlerin **ardına** çizilmeli, ki `Canvas`'ta çocuk sırası demek.
