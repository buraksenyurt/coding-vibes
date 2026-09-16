# Faz 5 — Mahalle Sınırları

> **Seri:** DockerCity — docker-compose topoloji dashboard'u
> **Önceki bölüm:** `04-canvas-ve-mvvm.md` · **Sonraki bölüm:** `06-etkilesim.md`
> **Tahmini süre:** 2–3 saat
> **Kod reposu:** `C:\Users\burak\Development\coding-vibes\DockerCity`

---

## 1. Bu bölümde ne yapacağız

Faz 4'te figürler doğru yerlere dizildi ama ortada bir "mahalle" yoktu; sadece iki grup vardı ve aralarındaki boşluktan ibarettiler.

Bu bölümde o grupları görünür kılıyoruz: her network kendi sınırıyla çevrelenmiş bir bölge oluyor, sol üstünde adı yazıyor. Örtük `default` ağı kesikli çizgiyle ayrışıyor.

Bir de Faz 2'den beri farkında olduğumuz ama ertelediğimiz duruma bakıyoruz: **bir servis birden fazla ağa üyeyse ne olur?**

---

## 2. Neden böyle

### 2.1 Canvas'ta derinlik, çocuk sırasından ibarettir

`Canvas`'ın `ZIndex` diye bir kavramı yoktur. Çocuklar `Children` koleksiyonuna eklendikleri sırayla çizilir; sonradan eklenen üste gelir.

Yani "bölgeler figürlerin ardında olsun" cümlesinin kodu şu kadar:

```csharp
// Order matters: districts first, so they sit behind the figures.
if (Districts is not null)
{
    foreach (var district in Districts) { ... Children.Add(visual); }
}

foreach (var node in Nodes) { ... Children.Add(visual); }
```

Bu kırılgan görünüyor ve öyle de. Alternatifi iki ayrı `Canvas`'ı bir `Grid` içinde üst üste koymaktı — katmanlar yapısal olarak ayrılırdı ve sıralama kazara bozulamazdı. Tek canvas'ta kalmayı seçtim çünkü Faz 7'de bağlantı okları gelecek ve onlar **bölgelerin üstünde, figürlerin altında** duracak. Üç katman için üç Canvas yönetmek, tek listede üç bölümü sırayla doldurmaktan daha çok iş.

Ama bu bir tercih, kural değil. Kod büyürse ayrı katmanlara geçmek mantıklı olur.

### 2.2 `IsHitTestVisible="False"`

`DistrictControl` bir dikdörtgen ve figürlerin üstünü kaplayacak kadar büyük. Fare olaylarını yutmasın diye:

```xml
<UserControl ... IsHitTestVisible="False">
```

Faz 6'da sürükleme geleceği için bu satır şimdiden orada. Olmazsa figürlere tıklayamazsın — ve sebebini bulmak, "tıklama neden çalışmıyor?" sorusunun en sinsi cevaplarından biridir.

### 2.3 Bir servis iki ağdaysa ne olur?

Faz 2'de not düşmüştük, Faz 4'te de `!nodes.ContainsKey(...)` satırıyla geçiştirmiştik: bir figür ikiye bölünemez, o yüzden ilk mahallesinde çiziliyor.

Ama Faz 4'teki uygulama bundan fazlasını yapıyordu — **ikinci mahalleyi haritadan tamamen düşürüyordu.** Bütün üyeleri başkası tarafından yerleştirilmiş bir district `continue` ile atlanıyor, hiç sınır üretmiyordu. Yani compose dosyanda iki ağ varsa ve biri diğerinin alt kümesiyse, o ağ ekranda hiç görünmüyordu.

Sessiz veri kaybı. Düzeltmesi iki parçalı:

**Sınırlar artık üyelerin gerçek konumlarından türetiliyor.** Önce blok düzeni yapılıyor, sonra her district için üyelerinin kapsayan dikdörtgeni hesaplanıyor:

```csharp
var points = district.Members
    .Where(member => nodes.ContainsKey(member.Name))
    .Select(member => nodes[member.Name])
    .ToList();

var left   = points.Min(point => point.X) - settings.DistrictPadding;
var top    = points.Min(point => point.Y) - settings.DistrictPadding;
var right  = points.Max(point => point.X) + settings.NodeWidth  + settings.DistrictPadding;
var bottom = points.Max(point => point.Y) + settings.NodeHeight + settings.DistrictPadding;
```

Böylece bir district, üyelerini kim yerleştirmiş olursa olsun hepsini kapsıyor.

**Hiç yerleştirme yapmamış district'ler "overlay" olarak işaretleniyor:**

```csharp
IsOverlay = !placedSomething.Contains(district.Name)
```

Overlay bölgeler dolgusuz ve kesikli çiziliyor — altındaki figürler başka bir mahalleye ait, onların üstünü renkle boyamak yanıltıcı olurdu.

> **Küçük ama önemli detay:** bu değişiklik mevcut testlerin hiçbirini bozmadı. Örtüşme olmayan durumda kapsayan dikdörtgen, blok hesabıyla **birebir aynı** sonucu veriyor. `Known_geometry_is_stable` testindeki 680×624 değerleri değişmeden kaldı. Refaktörün doğru yapıldığının kanıtı bu: davranış genişledi, mevcut davranış kıpırdamadı.

### 2.4 Renkler adla değil sırayla atanıyor

```csharp
public static Color For(int index, bool isImplicit) =>
    isImplicit ? Neutral : Hues[index % Hues.Length];
```

Ada göre hash'leyip renk seçmek cazip geliyor — "fnp-network hep mavi olsun". Ama iki dezavantajı var: yakın adlar yakın renkler üretebilir, ve bir ağı yeniden adlandırdığında rengi sebepsizce değişir.

Sırayla atamak, aynı dosyanın her açılışta aynı görünmesini garanti ediyor (Faz 4'te yerleşim sırasını neden sabitlediğimizle aynı gerekçe). Farklı dosyaların aynı rengi kullanması sorun değil, ikisi aynı anda ekranda değil.

Örtük ağ paletten renk almıyor: o bir tercih değil, artakalanlar. Nötr gri + kesikli çizgi.

---

## 3. Adım adım uygulama

### Adım 1 — `DistrictBounds`'a `IsOverlay`

```csharp
public sealed record DistrictBounds(
    string DistrictName,
    bool IsImplicit,
    double X, double Y, double Width, double Height)
{
    public bool IsOverlay { get; init; }
}
```

`init` özelliği olarak ekliyoruz, positional parametre olarak değil — böylece mevcut çağrı yerleri derlenmeye devam ediyor ve varsayılan `false` oluyor.

### Adım 2 — Motor: sınırları üyelerden türet

`GridCityLayoutEngine.Arrange` iki geçişe bölünüyor:

1. Yerleştirme: her district'in henüz yerleştirilmemiş üyelerini bir bloğa diz, hangi district'in gerçekten yerleştirme yaptığını `placedSomething` kümesinde tut.
2. Sınır hesabı: her district için `BoundsFor` çağır.

`BoundsFor` üyesi olmayan district için `null` döndürüyor, `OfType<DistrictBounds>()` onları eliyor. Boş bir ağ tanımlamak compose'da geçerlidir ve çizecek bir şey yoktur.

### Adım 3 — `DistrictViewModel`

Görsel kararlar burada:

```csharp
Fill = IsOverlay
    ? new SolidColorBrush(Color.FromArgb(0, 0, 0, 0))
    : new SolidColorBrush(Color.FromArgb(30, hue.R, hue.G, hue.B));

var dashes = new DoubleCollection();

if (IsImplicit || IsOverlay)
{
    dashes.Add(6);
    dashes.Add(4);
}
```

255 üzerinden 30 alfa, yaklaşık %12 opaklık. Yeterince belli olsun ama altındaki zemini yutmasın diye.

Etiket üç durumu ayırıyor:

```csharp
public string Label => (IsImplicit, IsOverlay) switch
{
    (true, _) => $"{Name} (implicit)",
    (_, true) => $"{Name} (shared)",
    _ => Name
};
```

> `DoubleCollection`'ı koleksiyon ifadesiyle (`[6, 4]`) kurmayı denedim, WinRT projeksiyonu olduğu için güvenmedim ve elle `Add` ettim. `Colors.Transparent` yerine `Color.FromArgb(0,0,0,0)` kullanmamın sebebi de benzer: WinUI'de `Colors` sınıfı `Microsoft.UI.Colors`, `Color` yapısı ise `Windows.UI.Color`. İkisini aynı dosyada kullanmak gereksiz karışıklık.

### Adım 4 — `DistrictControl`

`Border` yerine `Rectangle` kullanıyoruz, çünkü `Border`'ın kesikli kenarlığı yok. `Rectangle`'da `RadiusX`/`RadiusY` yuvarlak köşeyi, `StrokeDashArray` kesikli çizgiyi veriyor.

### Adım 5 — `CityCanvas`'a ikinci katman

İki bağımlılık özelliği artık aynı `OnSourceChanged` işleyicisini paylaşıyor. Abonelik `INotifyCollectionChanged` üzerinden yapılıyor, somut koleksiyon tipi üzerinden değil — iki farklı `ObservableCollection<T>` için tek kod.

`Rebuild` her iki katmanı da baştan kuruyor, `Detach` her ikisinin de aboneliklerini kaldırıyor.

### Adım 6 — Çalıştır

```powershell
dotnet test
```

Sonra F5 → fixture'ı aç. Sekiz figür mavi ve düz çizgili bir bölgede, ikisi gri ve kesikli bir bölgede olmalı.

---

## 4. Takıldığın yerler

**Bölgeler figürlerin üstünü kapatıyor.**
`Children.Add` sırası bozulmuş. Districts döngüsü Nodes döngüsünden önce olmalı.

**Figürlere tıklayamıyorum / tooltip çıkmıyor.**
`DistrictControl` üzerindeki `IsHitTestVisible="False"` düşmüş. Dikdörtgen fare olaylarını yutuyor.

**Kesikli çizgi görünmüyor.**
`StrokeDashArray` boş bir `DoubleCollection` ise düz çizgi çizilir — doğru davranış. Örtük olmayan bir ağda kesikli bekliyorsan `IsImplicit`/`IsOverlay` değerlerini kontrol et.

**Overlay bölge hiç görünmüyor.**
Dolgusu şeffaf, sadece kenarlığı var. Arkasındaki figürlerle aynı sınırlardaysa, alttaki bölgenin kenarlığıyla çakışıyor olabilir. `front.Width < back.Width` testinin geçtiğini doğrula.

**Renkler her açılışta değişiyor.**
Palet indeksi `city.Layout.Districts` listesindeki sıradan geliyor; o liste `OrderDistricts` ile sabitlenmiş olmalı. Faz 4'teki `Arrangement_is_repeatable` testi hâlâ geçiyor mu?

**Etiket bölgenin dışında kalıyor.**
`DistrictControl` içindeki `Grid`'in `Width`/`Height`'i ViewModel'den geliyor. Bağlama kopmuşsa Grid sıfır boyutlu olur ve etiket sol üstte havada durur.

---

## 5. Ne öğrendik

- **`Canvas`'ta derinlik, ekleme sırasıdır.** `ZIndex` yok. Bu bilgiyi bir yoruma yazmak, üç ay sonra "figürler neden bölgenin altında kaldı?" diye aramaktan ucuz.
- **Kaplayan bir görsel, fare olaylarını da kaplar.** `IsHitTestVisible="False"` dekoratif katmanların varsayılanı olmalı.
- **Sessiz veri kaybı, hatanın en pahalı türüdür.** Faz 4'te ikinci mahalle ekrandan düşüyordu ve hiçbir şey şikâyet etmiyordu. Testler bunu yakalamadı çünkü örnek dosyada o durum yoktu — **testin kapsamı, verinin kapsamı kadardır.**
- **Doğru refaktör mevcut testleri kıpırdatmaz.** Sınır hesabını blok matematiğinden kapsayan dikdörtgene çevirdik; davranış genişledi, çivilenmiş koordinatlar aynı kaldı.
- **Görsel kararları deterministik tut.** Renk ataması sıraya bağlı; hash'e ya da sözlük sırasına bağlı olsaydı aynı dosya iki açılışta farklı görünürdü.

---

## 6. Kendin dene

1. **İki ağa üye bir servis ekle.** Fixture'ın kopyasında `postgres`'i hem `fnp-network`'e hem yeni bir `data-network`'e üye yap. İkinci bölge nasıl çiziliyor? Etiketinde ne yazıyor? Bu görsel anlatım sence doğru mu, yoksa başka bir çözüm mü düşünürdün?

2. **Katmanları ayır.** `CityCanvas`'ı tek `Canvas` yerine bir `Grid` içinde iki `Canvas` olacak şekilde yeniden yaz. Kod daha mı okunaklı? Faz 7'de üçüncü katman (oklar) gelince hangisi daha kolay olur? Kararını bir cümleyle gerekçelendir.

3. **`IsHitTestVisible`'ı kaldır.** Satırı sil ve uygulamayı çalıştır. Tooltip'ler ne oldu? Bu, sorunu ilk kez yaşasaydın kaç dakikada bulurdun?

4. **Boş ağ.** Compose dosyasına hiçbir servisin kullanmadığı bir network ekle. Ne oluyor? `BoundsFor`'un `null` döndürmesi doğru davranış mı, yoksa boş bir bölge çizip "kimse yok" mu demeli?

5. **Dokuzuncu mahalle.** Palette sekiz renk var. Dokuz ağlı bir dosya açsan ne olur? `%` operatörü sorunu çözüyor mu, yoksa gizliyor mu?

---

**Sonraki bölüm:** `06-etkilesim.md` — **MVP'yi tamamlıyoruz.** Hover'da zengin detay balonu, tıklamayla seçim ve sağ panel, sürükle-bırak ve konumların `ServiceLayouts` tablosuna kaydı. Faz 3'te kurduğumuz `ILayoutStore` üç fazdır kullanılmayı bekliyor; yeri orası.
