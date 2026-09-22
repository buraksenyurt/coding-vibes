# Faz 7 — Yollar ve İlişkiler

> **Seri:** DockerCity — docker-compose topoloji dashboard'u
> **Önceki bölüm:** `06-etkilesim.md` · **Sonraki bölüm:** `08-kullanilabilirlik.md`
> **Tahmini süre:** 2–3 saat
> **Kod reposu:** `C:\Users\burak\Development\coding-vibes\DockerCity`

---

## 1. Bu bölümde ne yapacağız

`pgadmin → postgres` ilişkisi Faz 2'den beri `CityMap.Links` içinde duruyor. Beş fazdır tek bir piksel olarak bile ekrana çıkmadı.

Bu bölümde şehre yollar ekliyoruz:

- `depends_on` ilişkileri kavisli, ok başlı yollar olarak çiziliyor
- Paylaşılan named volume'ler kesikli yeşil çizgi olarak (örnek dosyada yok, ama hazır)
- Figür sürüklenince yol canlı takip ediyor
- Bir servis seçilince onun yolları belirginleşiyor, diğerleri soluklaşıyor
- Detay panelinde yeni bir satır: **Needed by** — `depends_on`'un tersi

---

## 2. Neden böyle

### 2.1 Geometri yine Domain'de

Faz 4'te yerleşim motorunu Domain'e koymuştuk, gerekçe test edilebilirlikti. Burada aynı karar, aynı gerekçe:

```
LinkRouter.Route(CityLink, düğüm konumları) → LinkPath?
```

`LinkPath` altı noktadan ibaret bir kayıt: başlangıç, iki kontrol noktası, bitiş, ok başının iki ucu. WinUI tarafı bu noktaları bir `PathGeometry`'ye kopyalamaktan başka bir şey yapmıyor.

Sonuç: yolun nereden başladığı, ne kadar kavis yaptığı, okun hangi yöne baktığı — hepsi pencere açmadan test ediliyor. `LinkRouterTests` içinde şunlar doğrulanıyor:

| Test | Neyi koruyor |
|---|---|
| Yol ikon çemberinin üstünde başlar ve biter | Ok figürün içine girmesin, havada da kalmasın |
| Ok başı bitiş noktasının **arkasında** | Ok ters yöne bakmasın |
| A→B ile B→A zıt yönlere kıvrılır | İki yönlü bağımlılıkta yollar üst üste binmesin |
| Birbirine değen ikonlar arasında yol yok | Tersine dönmüş bir kütük çizilmesin |
| Örnek dosyadaki her yol çizilebilir | Gerçek veriyle çalıştığı |

### 2.2 Yol figüre değil, ikona bağlanıyor

Bir figür 120×120'lik bir yuva ama bunun büyük kısmı etiket: servis adı, image, rozetler. Yolu figürün dikdörtgenine bağlasaydık ok çoğu zaman `postgres:latest` yazısına saplanırdı.

Bu yüzden yol, **ikonun merkezi etrafındaki bir çemberden** başlıyor:

```csharp
public double IconCenterX { get; init; } = 60;
public double IconCenterY { get; init; } = 41;
public double IconRadius { get; init; } = 46;
```

Bu üç değer `ServiceNodeControl.xaml`'daki ölçülerin aynası: 120 genişliğin ortası 60, 82'lik ikon ızgarasının ortası 41, yarıçap 41 + 5 piksel boşluk.

> **Bu bir bağımlılık ve bilerek kabul ediyoruz.** XAML'de ikonu büyütürsen `LayoutOptions`'ı da güncellemen gerekir. Alternatifi, çalışma zamanında görselin gerçek sınırlarını ölçüp Domain'e geri beslemekti — ama o zaman geometri testleri bir pencereye muhtaç olurdu. İki sabitin elle senkron tutulması, test edilebilirliğin bedeli.

### 2.3 Neden düz çizgi değil de kavis?

Üç sebep:

1. **Yön okunuyor.** Kavis, okun hangi uçta olduğunu bakmadan da hissettiriyor.
2. **Karşılıklı bağımlılıklar ayrışıyor.** `a → b` ve `b → a` düz çizgi olsaydı tam üst üste binerdi. Kavis hep hareket yönünün aynı tarafına kıvrıldığı için ikisi zıt taraflara açılıyor.
3. **Üçüncü bir figürün üstünden geçme ihtimali azalıyor** — çözmüyor, azaltıyor. Gerçek yol bulma (engelden kaçınma) bu projenin kapsamı dışında.

Kavis miktarı yolun uzunluğunun %18'i. Kısa yollar hafif, uzun yollar belirgin kıvrılıyor; sabit bir değer olsaydı kısa yollar yay gibi bükülürdü.

### 2.4 Ok başının yönü

İlk refleks, okun başlangıçtan bitişe olan düz çizgi yönünde çizilmesi. Ama yol kavisli; son kısmı o düz çizgiye paralel gelmiyor ve ok eğri durur.

Kübik Bezier eğrisinin güzel bir özelliği var: bitiş noktasına **ikinci kontrol noktasından gelen yönde** varır. Yani teğet, basitçe `End - Control2`:

```csharp
var tangentX = end.X - control2.X;
var tangentY = end.Y - control2.Y;
```

Ok başının iki kanadı bu teğetin tersine, ±26° açıyla çiziliyor.

### 2.5 Ok başı dolu değil, açık

Dolu bir üçgen daha "ok" gibi görünür. Ama WinUI'de `Path.Fill` tüm geometriye uygulanır; eğri açık bir figür olsa da, dolgu verildiğinde eğrinin altında kalan alan da boyanır.

İki çözüm vardı: ok başını ayrı bir `Path` olarak çizmek (her yol için iki görsel, iki senkronizasyon noktası) ya da açık bir ok başıyla yetinmek. İkincisini seçtik — `>` şeklinde, yolla aynı kalem.

### 2.6 `System.IO.Path` ile `Shapes.Path`

`CityCanvas.cs`'e `using Microsoft.UI.Xaml.Shapes;` eklediğin anda derleme belirsizlik hatası verir. Sebebi Faz 0'da: `Directory.Build.props` içinde `ImplicitUsings` açık ve implicit usings `System.IO`'yu getiriyor, dolayısıyla `Path` iki şey birden.

Çözüm bir takma ad:

```csharp
// Implicit usings bring in System.IO, whose Path would shadow the XAML shape.
using ShapePath = Microsoft.UI.Xaml.Shapes.Path;
```

Faz 0'da `UseImplicitUsings` yazım hatasını düzeltirken bir kolaylık açmıştık; bu da onun küçük bir faturası.

### 2.7 Seçim yolları da etkiliyor

```csharp
foreach (var link in Links)
{
    link.IsHighlighted = node is not null && link.Touches(node.Name);
    link.IsDimmed = node is not null && !link.IsHighlighted;
}
```

Örnek dosyada tek yol olduğu için bu fark az hissediliyor. Ama 30 servisli, 40 bağımlılıklı bir dosyada yollar bir spagetti olur; seçili servisin yollarını öne çıkarıp gerisini soluklaştırmak haritayı okunur tutuyor.

`LinkViewModel`'de iki `bool` ve iki türetilmiş özellik var:

```csharp
[ObservableProperty]
[NotifyPropertyChangedFor(nameof(Thickness), nameof(Opacity))]
private bool _isHighlighted;
```

Faz 6'da `SelectionOpacity` için kurduğumuz desenin aynısı.

### 2.8 "Needed by"

`depends_on` bir servisin neye ihtiyaç duyduğunu söyler. Tersini söylemez: **bu servis çökerse kim etkilenir?**

Operasyonel olarak çoğu zaman sorulan asıl soru ikincisi. Veritabanını yeniden başlatmadan önce ona bağımlı olanları bilmek istersin. Bilgi zaten `Links` içinde; sadece ters yönden okumak gerekiyordu:

```csharp
var neededBy = Links
    .Where(link => link.IsDirected && link.To == node.Name)
    .Select(link => link.From)
    ...
```

Yeni bir veri, yeni bir hesap yok. Yalnızca var olan bilgiye başka bir soru soruldu.

---

## 3. Adım adım uygulama

### Adım 1 — `LayoutOptions`'a ikon geometrisi

Üç yeni özellik, varsayılan değerleriyle. `init` oldukları için mevcut kullanımlar etkilenmiyor.

### Adım 2 — `LinkPath` ve `LinkRouter`

`LinkRouter` birincil kurucu (primary constructor) kullanıyor:

```csharp
public sealed class LinkRouter(LayoutOptions? options = null)
{
    private readonly LayoutOptions _options = options ?? LayoutOptions.Default;
    ...
}
```

`Route` null döndürebiliyor — iki durumda: bağlantının uçlarından birinin konumu yoksa, ya da ikonlar birbirine değiyorsa. Çağıran taraf null'ı "şimdilik çizme" olarak yorumluyor; figür uzaklaştırılınca yol yeniden beliriyor.

### Adım 3 — Testler

`tests/DockerCity.Domain.Tests/LinkRouterTests.cs` yedi test içeriyor. En öğretici olanı yön testi:

```csharp
static double Side(LayoutPoint origin, LayoutPoint toward, LayoutPoint point) =>
    ((toward.X - origin.X) * (point.Y - origin.Y)) - ((toward.Y - origin.Y) * (point.X - origin.X));

Assert.True(Side(a, b, forward.Control1) * Side(a, b, backward.Control1) < 0);
```

İki boyutlu çapraz çarpımın işareti, bir noktanın bir doğrunun hangi tarafında olduğunu söyler. İki işaretin çarpımı negatifse noktalar zıt taraflardadır. Grafik kodunda sık karşılaşacağın bir hile.

### Adım 4 — `LinkViewModel`

Rota, vurgu ve soluklaşma durumu. `Touches(serviceName)` yardımcı metodu seçim mantığını okunur tutuyor.

### Adım 5 — `CityCanvas`'a yol katmanı

Rebuild artık üç katman kuruyor:

```csharp
// Order matters: districts, then roads, then figures on top.
```

Yollar `Path` görselleri. Geometri canvas koordinatlarında olduğu için görselin kendisi `(0, 0)`'da duruyor. `IsHitTestVisible = false` — Faz 5'teki bölgeler gibi, yollar da tıklamaları yutmamalı.

Rota değişince geometri yeniden kuruluyor; vurgu değişince sadece kalınlık ve opaklık güncelleniyor.

### Adım 6 — Sürükleme

Faz 6'da `NodeMoved` sadece bölgeleri yeniden hesaplıyordu. Şimdi yolları da:

```csharp
foreach (var link in Links)
{
    link.Route = _router.Route(link.Link, positions);
}
```

Yol başına birkaç karekök. Her fare hareketinde yapılması sorun değil.

### Adım 7 — Çalıştır

```powershell
dotnet test
```

F5 → fixture'ı aç → `postgres`'i uzağa sürükle, yolun takip ettiğini gör.

---

## 4. Takıldığın yerler

**`'Path' is an ambiguous reference between 'System.IO.Path' and 'Microsoft.UI.Xaml.Shapes.Path'`**
Bkz. 2.6. `using ShapePath = ...;` takma adını kullan.

**Yol görünmüyor ama alt çubukta "1 links" yazıyor.**
Üç ihtimal: `CityBoard.Links` bağlanmamış; iki ikon birbirine değiyor ve `Route` null döndü; ya da yol bölgelerden **önce** eklenmiş ve bölgenin dolgusu altında kalmış. Rebuild'deki sırayı kontrol et.

**Ok ters yöne bakıyor.**
Tanjant `Control2 - End` olarak alınmış olabilir — işaret ters. Doğrusu `End - Control2`, ok kanatları da onun tersi yönünde.

**Ok yoldan kopuk, yanında duruyor.**
Ok başı düz çizgi yönünden hesaplanıyordur, eğri teğetinden değil. Bkz. 2.4.

**Yollar figürlere tıklamayı engelliyor.**
`IsHitTestVisible = false` eksik.

**Yol figürün üstünden geçiyor.**
Katman sırası yanlış — yollar figürlerden **önce** eklenmeli.

**İkonu büyüttüm, oklar ikonun içine giriyor.**
`LayoutOptions.IconRadius` ve `IconCenterY` XAML ile senkron değil. Bkz. 2.2.

---

## 5. Ne öğrendik

- **Bekleyen veri, bekleyen bir fırsattır.** `CityMap.Links` beş faz boyunca hazır durdu; bu fazda yeni bir şey hesaplanmadı, var olan görünür kılındı. "Needed by" da öyle — aynı listeye ters yönden bakmak.
- **Geometri test edilebilir.** Ok başının yönü, eğrinin tarafı, uçların çember üstünde olması — hepsi pencere açmadan doğrulanıyor.
- **Kübik Bezier'in teğeti bedava.** Bitişteki yön `End - Control2`. Grafik kodunda türev almadan doğru yönü bulmanın yolu.
- **Çapraz çarpımın işareti taraf söyler.** "Bu nokta doğrunun solunda mı sağında mı?" sorusunun en ucuz cevabı.
- **Her kolaylığın bir faturası var.** `ImplicitUsings` bir satır `using` tasarrufu sağladı, `Path` belirsizliğini de beraberinde getirdi.
- **İki yerde tutulan sabit, bilinçli bir borçtur.** İkon ölçüsü XAML'de ve `LayoutOptions`'ta. Bunu bir yoruma yazmak, üç ay sonra "oklar neden kaydı?" diye aramaktan ucuz.
- **Seçim, yoğunluğun panzehiridir.** Büyük bir haritada her şeyi eşit göstermek hiçbir şeyi göstermemektir.

---

## 6. Kendin dene

1. **Karşılıklı bağımlılık.** Fixture'ın kopyasına `postgres` için `depends_on: [pgadmin]` ekle (mantıksız ama öğretici). İki yol nasıl görünüyor? Kavis olmasaydı ne görürdün?

2. **Kavis sabitini oyna.** `Curvature`'ı 0, 0.18 ve 0.5 yap. Her birinde karşılıklı bağımlılık örneği nasıl görünüyor? Hangi değer hem okunur hem sade?

3. **Dolu ok başı.** Ok başını ayrı bir `Path` olarak, `Fill` ile çiz. `CityCanvas`'ta kaç satır kod eklendi, senkronizasyon noktası kaç oldu? Kazandığın görsel fark buna değer mi?

4. **Paylaşılan volume'ü gör.** Fixture'a aynı `postgres_data` volume'ünü kullanan ikinci bir servis ekle. Kesikli yeşil çizgi göründü mü? Ok başı neden yok?

5. **Döngü.** Faz 2'nin "kendin dene" bölümünde döngüsel bağımlılık tespitini önermiştik. Şimdi döngüyü görsel olarak da işaretle: döngüdeki yolları kırmızı çiz. Döngüyü kim bulmalı — `CityMapBuilder` mı, `LinkRouter` mı, ViewModel mi?

6. **Engelden kaçınma.** Bir yol üçüncü bir figürün üstünden geçiyorsa ne yapardın? Yolu figürün etrafından dolaştırmak için bir fikir yaz — kodlamak zorunda değilsin, ama maliyetini tahmin et.

---

**Sonraki bölüm:** `08-kullanilabilirlik.md` — plan değişti: zoom/pan'dan önce uygulamayı kullanışlı hale getiriyoruz. Menü çubuğu, son açılan dosyalar, dosya değişince uyarı, tema, PNG dışa aktarma ve About penceresi. Veritabanı şeması da ilk kez değişiyor.
