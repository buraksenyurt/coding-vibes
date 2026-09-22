# Faz 9 — Zoom, pan ve animasyon

> **Seri:** DockerCity — docker-compose topoloji dashboard'u
> **Önceki bölüm:** `08-kullanilabilirlik.md` · **Sonraki bölüm:** `10-canli-docker.md`
> **Tahmini süre:** 4–5 saat
> **Kod reposu:** `C:\Users\burak\Development\coding-vibes\DockerCity`

---

## 1. Bu bölümde ne yapacağız

Şehir artık kullanışlı; bu bölümde onu **dolaşılabilir** ve biraz da **canlı** hale getiriyoruz:

| Özellik | Nasıl |
|---|---|
| Yakınlaştırma / uzaklaştırma | Ctrl+tekerlek, touchpad'de iki parmak, Ctrl+Artı / Ctrl+Eksi, View menüsü |
| Gerçek boyut ve pencereye sığdır | Ctrl+0, Ctrl+9, durum çubuğundaki `%` düğmesi |
| Boş zemini sürükleyerek kaydırma (pan) | Sol ya da orta fare tuşu |
| Mini harita | Sağ altta; tıkla ya da sürükle, görünüm oraya gider. Ctrl+4 |
| Giriş animasyonu | Mahalleler büyür, figürler sırayla "zıplayarak" belirir, yollar en son döşenir |
| Hover'da zıplama | Figürün ikonu yaylı bir animasyonla büyür |
| Nöbetçi rozeti | `restart: always` / `unless-stopped` olan servisin ikonunda ↻ işareti |

Hesap tarafı yine Domain'de ve testli: `ZoomMath`, `MinimapProjection`, `LayoutBounds`.

---

## 2. Neden böyle

### 2.1 ScrollViewer mı, CompositeTransform mı?

Planda iki yol yazıyordu. İkisini de tarttık:

| | `ScrollViewer` zoom | Canvas'a `CompositeTransform` |
|---|---|---|
| Ctrl+tekerlek, touchpad pinch, dokunmatik | Hazır geliyor | Hepsini elle yazmak gerekiyor |
| Kaydırma çubukları | Zoom'la uyumlu, hazır | Transform'dan habersiz; ayrıca hesaplanmalı |
| Tekerleğin imleç etrafında zoom yapması | Hazır | Elle: imlecin dünya koordinatını sabit tutan ofset hesabı |
| Kontrol | `ChangeView` ile sınırlı | Tam |

`ScrollViewer` kazandı. Zoom'u bir **görünüm** meselesi olarak ele alıyor: canvas'ın içindeki hiçbir şey zoom'dan haberdar değil. Figürlerin `X`/`Y` değerleri, yolların Bezier noktaları, mahalle sınırları hep **dünya koordinatında** kalıyor. Zoom, üstlerine giydirilmiş bir gözlük.

```xml
<ScrollViewer x:Name="CityScroller"
              ZoomMode="Enabled"
              MinZoomFactor="0.25"
              MaxZoomFactor="3" ...>
```

Sınırlar `ZoomMath.Minimum` / `Maximum` ile aynı. İkisi ayrı yerde yazılı; biri değişirse diğeri de değişmeli. (Bunu bir testle bağlamak mümkün değil, çünkü XAML değeri test projesinden görünmüyor. Bir yorum ve bu paragraf, şimdilik sigorta.)

### 2.2 Öngörü yanlıştı: sürükleme bozulmadı

Faz 8'in sonunda şöyle yazmıştık: *"Canvas ölçeklendiğinde `GetCurrentPoint(this)` artık dünya koordinatı değil, her şeyin yeniden hesaplanması gerekiyor."*

Yanlıştı. `GetCurrentPoint(element)` konumu **o elemanın kendi koordinat sisteminde** verir; atalarındaki ölçekleme de dahil bütün dönüşümleri geri çevirerek. Canvas `%200`'de çizilirken fare 10 ekran pikseli kaydığında, `GetCurrentPoint(canvas)` 5 birim kaydığını söyler. Figür de 5 dünya birimi kayar, ekranda yine 10 piksel. İmlecin altında kalır.

Yani Faz 6'daki sürükleme kodu **tek satır değişmeden** zoom altında doğru çalışıyor. Bozulan başka bir şey oldu; bir sonraki başlık o.

Buradan çıkan ders: *"Bu şu yüzden bozulacak"* dediğin bir şeyi, düzeltmeye girişmeden önce dene. Tahmin ettiğin hata bazen hiç yoktur, asıl hata ise tahmin etmediğin yerdedir.

### 2.3 Asıl tuzak: kaydırırken kayan zemin

Fareyle boş zemini sürükleyip şehri kaydırmak istiyoruz. İlk akla gelen, sürüklemeyle aynı kalıp:

```csharp
// YANLIŞ
var p = args.GetCurrentPoint(CityBoard).Position;
CityScroller.ChangeView(origin.X - (p.X - start.X), ...);
```

Çalıştırınca görüntü titrer, hatta kaçar. Neden? `ChangeView` canvas'ı kaydırır; canvas imlecin altından kayar; bir sonraki `PointerMoved`'da imlecin **canvas'a göre** konumu değişmiş olur, fare hiç kıpırdamasa bile. Kaydırma kendi girdisini değiştiriyor: bir geri besleme döngüsü.

Çözüm: konumu **kıpırdamayan** bir elemana göre ölç. `ScrollViewer`'ın kendisi yerinde durur:

```csharp
var position = args.GetCurrentPoint(CityScroller).Position;

CityScroller.ChangeView(
    _panOriginX - (position.X - _panStart.X),
    _panOriginY - (position.Y - _panStart.Y),
    null,
    disableAnimation: true);
```

Bir güzel yan etki: ofsetler de imleç de **zoom'lu pikselde**, yani her zoom seviyesinde fare bir piksel kaydığında şehir de bir piksel kayar. Ölçekle çarpıp bölmeye gerek yok.

Sürükleme ile kaydırma bu yüzden farklı koordinat sistemleri kullanıyor, ve ikisi de doğru:

| İş | Neye göre ölçülür | Neden |
|---|---|---|
| Figür sürükleme | Canvas (`GetCurrentPoint(this)`) | Figürün yeri dünya koordinatında; zoom otomatik geri çevriliyor |
| Zemin kaydırma | ScrollViewer | Kaydırma canvas'ı hareket ettiriyor; sabit bir referans gerek |

### 2.4 Görünmez zemin

Kaydırmayı yazınca bir sürpriz daha: boş zemine tıklamak hiçbir şey yapmıyordu. Faz 6'dan beri "boş canvas'a tıklayınca seçim temizlenir" diye bir satırımız vardı:

```csharp
CityBoard.PointerPressed += (_, _) => _viewModel.Select(null);
```

Bu satır **hiç çalışmamış.** `Canvas`'ın varsayılan `Background`'u `null`; `null` arka planlı bir panel, çocuklarının olmadığı yerde fare için yok hükmündedir. Tıklama arkadaki `CityFrame` Border'ına düşüyordu. Kimse fark etmedi, çünkü seçimi temizlemenin bir de Esc yolu vardı.

Düzeltme tek satır: **şeffaf** fırça, `null` değil.

```csharp
Background = new SolidColorBrush(Color.FromArgb(0, 0, 0, 0));
```

Görsel olarak ikisi aynı; isabet testi (hit testing) açısından biri var, biri yok. WinUI'de (ve WPF'te) bilinen bir tuzak, ve bir özelliği çalışıyor sanıp test etmemenin güzel bir örneği.

### 2.5 Zoom komutları neden merkezi korumalı?

`ChangeView(x, y, zoom)` yeni ofsetleri **yeni zoom'un pikselleriyle** ister. Eski ofsetleri verip sadece zoom'u değiştirirsen, zoom sol üst köşe etrafında yapılır; ekranın ortasındaki figür kaçar.

`ZoomMath.OffsetsKeepingCenter` önce pencerenin ortasının dünya koordinatını bulur, sonra o noktayı yeni zoom'da ortaya koyan ofseti hesaplar:

```
merkezDünya = (ofset + viewport/2) / eskiZoom
yeniOfset   = merkezDünya × yeniZoom − viewport/2      (en az 0)
```

Ctrl+tekerlek bunu zaten kendisi yapıyor (hem de imleç etrafında); bu hesap menü ve klavye için.

### 2.6 Zoom merdiveni

Ctrl+Artı her basışta `× 1.1` yapsaydı: 1.1, 1.21, 1.331, 1.4641... Durum çubuğunda `%146` gibi garip sayılar ve bir daha asla tam `%100`'e dönememek. Tarayıcılar bu yüzden sabit basamaklar kullanır:

```
0.25 · 0.33 · 0.5 · 0.67 · 0.75 · 0.9 · 1 · 1.1 · 1.25 · 1.5 · 1.75 · 2 · 2.5 · 3
```

`StepIn` bir sonraki, `StepOut` bir önceki basamağa gider. Ctrl+tekerlekle `%137`'ye gelinse bile, bir Ctrl+Artı `%150`'ye oturtur. Küçük bir tolerans (`1e-6`) var, çünkü `ZoomFactor` bir `float` ve `0.33f`, `double` olarak `0.33000001...`'dir.

### 2.7 "Sığdır" neden büyütmüyor?

`ZoomMath.Fit` şehri pencereye sığacak kadar **küçültür**, ama küçük bir şehri asla büyütmez; üst sınırı `1`. On figürlük bir şehri `%300`'e şişirmek onu büyütür, daha anlaşılır yapmaz. Ayrıca yeni bir dosya açıldığında `Fit` çağrılıyor: şehir sığıyorsa `%100`'de açılıyor, sığmıyorsa bütünü görünecek kadar küçülüyor.

Sığdırma **canvas'ın** değil **içeriğin** boyutuna göre yapılıyor. Canvas hiçbir zaman 800×600'den küçük olmuyor (Faz 6'da sürüklerken zıplamasın diye); ona sığdırmak, küçük bir şehri boşluğun ortasında bırakırdı. İçeriğin gerçek kutusu için Domain'e `LayoutBounds` geldi: figürlerin ve mahalle sınırlarının birleşimi.

Faz 8'in "Kendin dene" listesindeki 5. alıştırma (PNG'yi içeriğe kırp) için aradığın şey de tam olarak bu tip.

### 2.8 Mini harita: iki koordinat sistemi, bir sınıf

Mini harita 220×150'lik bir panel. İçine şehrin tamamı, en-boy oranı korunarak ve ortalanarak çiziliyor. Dönüşümün iki yönü var:

- **Dünya → harita:** kutuları çizmek ve "şu an burayı görüyorsun" çerçevesini yerleştirmek için.
- **Harita → dünya:** haritaya tıklanınca görünümü oraya götürmek için.

İkisi de `MinimapProjection`'da; tek bir `Scale` ve iki ofset. Test tarafı basit: bir noktayı gidip geri getir, aynı nokta dönmeli.

Görünüm çerçevesi, `ScrollViewer`'ın zoom'lu ofsetlerinden dünyaya çevrilerek bulunuyor:

```csharp
Minimap.ShowViewport(
    CityScroller.HorizontalOffset / zoom,
    CityScroller.VerticalOffset / zoom,
    CityScroller.ViewportWidth / zoom,
    CityScroller.ViewportHeight / zoom);
```

Her sürüklemede mini harita **baştan** çiziliyor. Bir şehirde onlarca şekil var, binlerce değil. Eleman eleman senkronize tutmak (CityCanvas'ta yaptığımız gibi) burada kazandırdığından fazlasını karmaşıklık olarak geri alırdı.

### 2.9 Üç animasyon, üç teknik

Giriş animasyonu tek bir şey gibi görünüyor ama üç ayrı araç kullanıyor, çünkü üç ayrı kısıt var.

**Mahalleler ve figürler — composition "facade" animasyonu.** WinUI 3'te her `UIElement`'in `Scale`, `Translation`, `CenterPoint` gibi özellikleri doğrudan compositor'a bağlı. `UIElement.StartAnimation` ile bunlara composition animasyonu verilebiliyor:

```csharp
var scale = compositor.CreateVector3KeyFrameAnimation();
scale.Target = "Scale";
scale.InsertKeyFrame(0f, new Vector3(0.01f, 0.01f, 1f));
scale.InsertKeyFrame(1f, Vector3.One, easing);
scale.DelayTime = delay;
scale.DelayBehavior = AnimationDelayBehavior.SetInitialValueBeforeDelay;

visual.StartAnimation(scale);
```

Neden composition? Animasyon **compositor iş parçacığında** koşuyor; UI iş parçacığı o sırada dosya kaydediyor ya da recent listesini yeniliyor olsa bile takılmıyor.

`SetInitialValueBeforeDelay` önemli: onsuz, sırası gelmemiş figür tam boyutta bekler, sırası gelince bir anda küçülüp büyür. Onunla, 0. anahtar karede (neredeyse görünmez) bekler.

Easing: `(0.34, 1.56) – (0.64, 1)` kübik Bezier, "ease out back". Hedefi biraz aşıp geri oturuyor; kayma değil **zıplama** gibi okunmasının sebebi bu.

Sıralama okuma düzeninde (önce `Y`, sonra `X`); adım `min(45 ms, 700 ms / (n−1))`. Servis sayısı ne olursa olsun gösteri bir saniyenin altında kalıyor.

**Yollar — Storyboard, `FillBehavior.Stop`.** Yolların `Scale`'le büyümesi anlamsız; solarak gelmeleri gerek. Ama `Opacity` zaten sahipli: seçim vurgusu (Faz 7) onu kullanıyor. Bir `Storyboard` `FillBehavior.Stop` ile özelliği animasyon boyunca **ödünç alıyor**, bitince yerel değere, yani vurgunun belirlediği değere geri bırakıyor:

```csharp
var fade = new DoubleAnimation
{
    From = 0,
    To = visual.Opacity,
    Duration = new Duration(TimeSpan.FromMilliseconds(300)),
    FillBehavior = FillBehavior.Stop
};
```

`HoldEnd` (varsayılan) olsaydı, animasyon değeri yerel değerin önüne geçer ve seçim vurgusu bir daha yollara ulaşamazdı. Yollar, son figür indiğinde bir zamanlayıcıyla (`DispatcherQueueTimer`) görünür yapılıyor: önce inşaat, sonra yollar.

**Hover — yay (spring) animasyonu.** Hover'ın süresi belli değil: fare 80 ms sonra çıkabilir. Süreli bir animasyonu yarıda kesip tersine çevirmek sıçrama yaratır. Yayın süresi yok; sertliği (`Period`) ve sönümü (`DampingRatio`) var. Yarıda yeni bir hedef verilince, bulunduğu yerden yeni hedefe doğru devam ediyor:

```csharp
_spring.FinalValue = new Vector3(scale, scale, 1f);
IconHost.StartAnimation(_spring);
```

Sadece ikon (`IconHost`) büyüyor, etiketler değil; fare üstündeyken yazı okunur kalsın diye. Giriş animasyonu figürün kendisinin `Scale`'ini, hover ise ikonun `Scale`'ini kullanıyor; ikisi aynı özelliğe dokunmadığı için çakışmıyorlar.

### 2.10 Animasyonu kapatmış kullanıcı

Windows'ta *Ayarlar › Erişilebilirlik › Görsel efektler › Animasyon efektleri* diye bir anahtar var. Bazı insanlar onu, hareket onları rahatsız ettiği için kapatır. Bu ayar `UISettings.AnimationsEnabled` ile okunuyor; dekoratif her animasyon önce `Motion.IsEnabled`'a soruyor. Kapalıysa şehir bir anda beliriyor, hover'da bir şey olmuyor, zoom komutları anında geçiyor.

Bu bir süs değil. Oyunlaştırma "cilası" dediğimiz şey, isteyene cila, istemeyene engel olmamalı.

### 2.11 Nöbetçi rozeti

Faz 4'ten beri figürün altında `restarts` diye soluk bir metin rozeti vardı. Artık ikonun sağ üst köşesinde, vurgu renginde küçük bir ↻ var (Segoe Fluent Icons, `E72C`). Üstüne gelince `Sentinel · restart: always` ya da `unless-stopped` yazıyor.

`on-failure` rozet almıyor: o servis çökünce kalkar, ama elle durdurulunca kalkmaz. Nöbetçi değil, sigortalı. Bu ayrım zaten Faz 2'deki `ComposeService.IsSupervised`'da yapılmıştı; ViewModel sadece aktardı.

### 2.12 Koleksiyon değişince her şeyi baştan kurmamak

Faz 4'ten beri `CityCanvas`, koleksiyonda **herhangi bir** değişiklik olunca bütün çocukları silip baştan kuruyordu. Bir dosya yüklenirken 10 figür, 6 mahalle, 12 yol tek tek eklendiği için bu 28 kez yeniden kurmak demekti. Küçük şehirde hissedilmiyordu.

Giriş animasyonu bunu görünür kıldı: animasyon hangi elemanlara uygulanacaksa, onların **son** yeniden kurulumdan kalanlar olması gerek. Ekleme artık yerinde yapılıyor, her katman kendi derinliğine:

```csharp
Children.Insert(_districtVisuals.Count, visual);                       // mahalle
Children.Insert(_districtVisuals.Count + _linkVisuals.Count, visual);  // yol
Children.Add(visual);                                                  // figür
```

`Clear` ya da `Remove` gibi seyrek durumlar hâlâ baştan kuruluyor. Her durumu akıllıca ele almak yerine sık olanı hızlı, seyrek olanı basit tutmak.

### 2.13 Yeniden yükleme yerini korur

Yeni bir dosya açılınca: sığdır, animasyonu oynat. **Aynı** dosya yeniden yüklenince (F5, dışarıdan değişiklik, Reset layout): hiçbir şey. Kullanıcı `%200`'de bir köşeye bakarken dosyayı kaydetti diye onu uzaklaştırmak, her kayıtta bir gösteri izletmek can sıkıcı olurdu. Bu ayrımı ViewModel'in `CityLoaded` olayı taşıyor (`IsSameFile`).

Olay, koleksiyonlar dolduktan hemen sonra, **hiçbir `await`'ten önce** tetikleniyor. Arada bir `await` olsaydı, figürler bir kare tam boyutta görünür, sonra animasyon onları küçültüp yeniden büyütürdü.

Sığdırma ise bir kare sonra, düşük öncelikle çalışıyor: `ScrollViewer` karşılama ekranı yüzünden az önce görünmezdi ve henüz ölçülmedi; o an `ViewportWidth` sıfır okunur.

```csharp
DispatcherQueue.TryEnqueue(DispatcherQueuePriority.Low, () => FitToWindow(animate: false));
```

---

## 3. Adım adım uygulama

### Adım 1 — Domain: hesaplar

`src/DockerCity.Domain/Layout/` altına:

- `LayoutBounds.cs` — içeriğin kutusu. `Of(CityLayout)` figürleri (`NodeWidth` × `NodeHeight`) ve mahalleleri birleştirir.
- `ZoomMath.cs` — `Clamp`, `StepIn`, `StepOut`, `Fit`, `OffsetsKeepingCenter`, `OffsetsToCenterOn`.
- `MinimapProjection.cs` — `ToMinimap`, `ToWorld`.

Testler: `ZoomMathTests`, `MinimapProjectionTests`, `LayoutBoundsTests`; `CityLayoutTests`'e örnek dosyanın içerik kutusu (`32, 32, 648, 592`).

Bir kontrol değeri: 2000×1000'lik içerik, 1000×600'lük pencerede, 24 px kenar boşluğuyla: `min(952/2000, 552/1000) = 0.476`.

### Adım 2 — ViewModel

`MainViewModel`:

- `ContentBounds` — yüklemede ve her sürüklemede güncellenir.
- `ShowMinimap` + `IsMinimapVisible`; tercih anahtarı `view.minimap` (`AppPreferences.ShowMinimapKey`).
- `CityLoaded` olayı, `CityLoadedEventArgs.IsSameFile` ile.

`ServiceNodeViewModel`: `IsSupervised`, `SupervisedText`; `restarts` metin rozeti kalktı.

### Adım 3 — CityCanvas

- Şeffaf `Background`.
- Yerinde ekleme (`AddDistrict`, `AddLink`, `AddNode`).
- `PlayEntrance()` ve yollar için `FadeRoadsIn()`.
- `SetPanCursor(bool)` — `ProtectedCursor` yalnızca sınıfın içinden ayarlanabildiği için pencere bunu doğrudan yapamıyor.

### Adım 4 — Mini harita ve figür

- `Controls/MinimapControl.cs` — `Render`, `ShowViewport`, `NavigateRequested`.
- `ServiceNodeControl.xaml` — `IconHost` adı, nöbetçi rozeti, `PointerEntered`/`PointerExited`.
- `ServiceNodeControl.xaml.cs` — yay animasyonu.
- `Services/Motion.cs` — animasyon ayarı.

### Adım 5 — Pencere

`MainWindow.xaml`:

- `ScrollViewer` → `CityScroller`, `ZoomMode="Enabled"`.
- View menüsüne *Mini map*, *Zoom in/out*, *Actual size*, *Fit to window*.
- Sağ altta mini harita; `ScrollViewer`'ın **dışında**, yoksa o da zoom'lanırdı.
- Durum çubuğunun sağında zoom yüzdesi; tıklayınca `%100`.

`MainWindow.xaml.cs`:

- Zoom kısayolları kodda (`AddZoomAccelerators`). Ana klavyedeki Artı ve Eksi, `VirtualKey`'de adı olmayan OEM tuşları (187 ve 189); XAML'de yazılamıyorlar.
- `ZoomTo`, `FitToWindow`, `CenterOn`.
- Zemin kaydırma: `OnBoardPointerPressed/Moved/Released`, `EndPan`.
- `OnCityLoaded`, `UpdateViewIndicators`, `RenderMinimap`.

`Windows.System` isim alanı bütünüyle eklenmedi: onun da kendi `DispatcherQueue` ve `DispatcherQueuePriority` tipleri var, `Microsoft.UI.Dispatching`'dekilerle çakışıyorlar. Sadece `VirtualKey` ve `VirtualKeyModifiers` takma adla alındı.

### Adım 6 — Çalıştır ve dene

```powershell
dotnet test
dotnet build src\DockerCity.App -p:Platform=x64
```

Kontrol listesi:

1. Dosya aç: mahalleler büyüyor, figürler sırayla zıplıyor, yollar en son soluyor.
2. Ctrl+tekerlek imleç etrafında zoom yapıyor; durum çubuğundaki yüzde değişiyor.
3. Ctrl+Artı / Ctrl+Eksi (ya da sayısal tuş takımı) basamak basamak ilerliyor; pencerenin ortası yerinde kalıyor.
4. `%200`'de bir figürü sürükle: imlecin altında kalıyor mu?
5. Boş zemini sürükle: şehir kayıyor, imleç dört yönlü oka dönüyor. Tek tıklama hâlâ seçimi temizliyor.
6. Mini haritaya tıkla ve sürükle; çerçeve görünümü takip ediyor.
7. Ctrl+9 sığdırır, Ctrl+0 `%100`'e döner, Ctrl+4 mini haritayı gizler; kapat-aç, tercih hatırlanıyor.
8. F5: zoom ve konum korunuyor, animasyon oynamıyor.
9. `restart: always` olan servislerde ↻ rozeti; üstüne gelince açıklama.
10. Windows'ta animasyon efektlerini kapat, dosyayı yeniden aç: animasyon yok.
11. `%50`'deyken PNG dışa aktar: resim hangi ölçekte çıkıyor?

---

## 4. Takıldığın yerler

**Türkçe Q klavyede Ctrl+Artı çalışmıyor.** Ana klavyede `+`, `Shift+4`'te; ayrı bir tuşu yok. 187 numaralı OEM tuşu, İngilizce düzende `=`/`+` tuşu. Türkçe Q'da sayısal tuş takımının `+`/`-` tuşları ve Ctrl+tekerlek çalışıyor. Tarayıcılar bunu tuşu değil karakteri dinleyerek çözüyor; `KeyboardAccelerator` tuş seviyesinde çalıştığı için aynı şeyi yapamıyor. Kendin dene bölümünde bunun için bir alıştırma var.

**`ChangeView` hiçbir şey yapmıyor.** `ScrollViewer` henüz ölçülmemişse (görünmezken, ya da ilk yerleşimden önce) çağrı sessizce yok sayılır ya da `ViewportWidth = 0` ile yanlış hesaplanır. Yüklemeden hemen sonra sığdırmanın bir kare ertelenmesinin sebebi bu.

**`CompositionTarget` belirsiz referans.** Hem `Microsoft.UI.Composition` hem `Microsoft.UI.Xaml.Media` bu adda bir sınıf tanımlıyor. Compositor'ı veren `Microsoft.UI.Xaml.Media.CompositionTarget.GetCompositorForCurrentThread()`. Takma adla (`XamlCompositionTarget`) çözüldü.

**`ElementCompositionPreview.GetElementVisual` ile facade karışmaz.** Bir elemanın "hand-in" görselini alıp `Offset`/`Scale`'ini değiştirirsen, aynı elemanın `UIElement.Scale` facade'ı ile çatışırsın. Bu fazda hep facade kullanıldı; `GetElementVisual` hiç çağrılmadı.

**Animasyon bitince değer ne olur?** Facade animasyonları XAML özelliğinin değerini geri yazmıyor olabilir. Bu yüzden her animasyon özelliğin **varsayılan** değerinde bitiyor (`Scale = 1`, `Translation = 0`); geri yazılsa da yazılmasa da sonuç aynı.

---

## 5. Ne öğrendik

- **Zoom'u görünüme bırak.** Dünya koordinatları zoom'dan habersiz kaldıkça sürükleme, yollar, kalıcı konumlar ve testler hiç değişmedi.
- **Öngörüyü dene.** "Zoom sürüklemeyi bozacak" dedik; bozmadı. Bozulan, hiç düşünmediğimiz kaydırmaydı.
- **Referans elemanı dikkatli seç.** Hareket eden şeye göre ölçülen hareket, kendi kendini besleyen bir döngüdür.
- **`null` arka plan ile şeffaf arka plan aynı şey değil.** Biri isabet testine var, biri yok. Faz 6'dan beri çalışmayan bir satır ancak bu fazda ortaya çıktı.
- **Hesap Domain'de, çizim App'te.** Zoom merdiveni, sığdırma, merkezi koruma, mini harita izdüşümü: hepsi testli, hiçbiri WinUI bilmiyor.
- **Her animasyon kısıtı ayrı bir araç seçtirir.** Composition (akıcılık), Storyboard + `FillBehavior.Stop` (sahipli bir özelliği ödünç almak), yay (süresi belli olmayan etkileşim).
- **Erişilebilirlik ayarına saygı.** Animasyon bir tercih; kapatana dayatılmamalı.
- **Aynı dosya, aynı yer.** Yeniden yükleme, kullanıcının baktığı yeri değiştirmemeli.

---

## 6. Kendin dene

1. **Karakteri dinle.** Türkçe Q klavyede Ctrl+Artı'nın çalışması için `RootGrid`'e `CharacterReceived` ekleyip Ctrl basılıyken gelen `+` karakterini yakala. Bu, `KeyboardAccelerator`'dan önce mi sonra mı çalışıyor? Ctrl basılıyken karakter gelir mi?

2. **İmleç etrafında zoom.** Ctrl+Artı şu an pencerenin ortası etrafında zoom yapıyor. İmleç canvas'ın üstündeyse onun etrafında yapsın. `ZoomMath`'e hangi fonksiyonu eklersin? (İpucu: `OffsetsKeepingCenter`'daki `viewport/2`, aslında imlecin viewport içindeki konumu.) Önce testini yaz.

3. **Zoom'u hatırla.** Her proje için son zoom ve ofseti `AppSettings`'e ya da `ComposeProjects` tablosuna yaz; dosya yeniden açılınca oradan başlasın. Hangisi daha doğru yer? Migration gerekir mi?

4. **Nöbetçiye hayat ver.** ↻ rozeti durağan. Her birkaç saniyede bir yavaşça dönmesini sağla. Composition'da sonsuz tekrar (`IterationBehavior.Forever`) ve `RotationAngleInDegrees`. On nöbetçi aynı anda dönerse göz yorar mı? Aralarına rastgele gecikme koy.

5. **PNG'yi içeriğe kırp.** `LayoutBounds` hazır. Faz 8'in 5. alıştırmasını şimdi yap.

6. **Mini haritada seçim.** Seçili figür mini haritada vurgu rengiyle, biraz büyük görünsün. `Render` her şeyi baştan çizdiği için bu kolay; seçim değişince de yeniden çizmeyi unutma.

---

**Sonraki bölüm:** `10-canli-docker.md` — şehir şimdiye kadar bir dosyanın resmiydi. Docker Engine API'sine bağlanıp hangi container'ın gerçekten çalıştığını gösterdiğimizde, bir haritadan bir kontrol paneline dönüşecek.
