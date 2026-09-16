# Faz 6 — Etkileşim *(MVP tamamlanır)*

> **Seri:** DockerCity — docker-compose topoloji dashboard'u
> **Önceki bölüm:** `05-mahalle-bolgeleri.md` · **Sonraki bölüm:** `07-baglantilar.md`
> **Tahmini süre:** 3–4 saat
> **Kod reposu:** `C:\Users\burak\Development\coding-vibes\DockerCity`

---

## 1. Bu bölümde ne yapacağız

Şehir duruyor ama ölü. Bu bölümde canlanıyor:

- Figürün üzerine gelince bilgi balonu
- Tıklayınca seçim ve sağda tam detay paneli
- Sürükle-bırak, mahalle sınırı figürü takip ederek
- Konumların veritabanına kaydı ve yeniden açılışta geri gelmesi

Sonunda **MVP tamam.** Faz 0'da koyduğumuz kabul kriterlerinin tamamı karşılanmış olacak.

---

## 2. Neden böyle

### 2.1 Planda söz verdiğim özel `Popup`'ı yazmadım

Planda şöyle yazmıştım: *"`ToolTip` yerine özel `Popup` — 300ms gecikme, imleç takibi, zengin içerik."*

Yazmadım. Gerekçe:

Özel bir `Popup`, gösterme/gizleme zamanlayıcılarını, imleç takibini, ekran kenarına taşma durumunu ve odak davranışını elle yönetmek demek. Bunların hepsi `ToolTip`'te zaten var ve doğru çalışıyor. Yeniden yazmanın karşılığında elde edeceğim tek şey daha zengin bir görsel düzendi.

Ama **zengin detayın asıl yeri balon değil, sağ panel.** Balonun işi "bu hangi servisti?" sorusunu fareyi oynatmadan cevaplamak; bunun için beş satır metin yeterli. Panel açıldığında portlar, volume'ler, ortam değişkenleri, komut — hepsi orada, okunaklı biçimde.

Yani özellik düşmedi, doğru yere taşındı. Balon `TooltipText` adında çok satırlı bir metin:

```csharp
text.AppendLine(_service.Name);
text.AppendLine(ImageLabel);
text.AppendLine($"container: {ContainerText}");
text.AppendLine($"networks: {NetworksText}");
```

> **Bir teknik gerekçe daha:** `ToolTip` içeriği ayrı bir görsel ağaçta (popup) yaşar ve `DataContext` mirasının oraya nasıl aktığı WinUI'de net değil. Metin bağlaması her koşulda çalışır. Denemeden emin olamayacağım bir şeyi öğreti dokümanına koymak istemedim.

### 2.2 Seçim neden basış anında?

```csharp
visual.CapturePointer(args.Pointer);
NodeSelected?.Invoke(this, node);
```

Bırakma anında seçseydik, sürüklenen figür sürükleme boyunca seçili olmazdı ve sağ panel boş kalırdı. Basışta seçmek hem tıklamayı hem sürüklemeyi doğru karşılıyor.

### 2.3 Sürükleme eşiği

```csharp
private const double DragThreshold = 4;

if (!_dragMoved && Math.Abs(deltaX) + Math.Abs(deltaY) < DragThreshold)
{
    return;
}
```

Bu dört piksel olmadan her tıklama bir sürükleme sayılır ve her tıklama veritabanına yazma tetikler. Fare hiç kıpırdamadan tıklamak neredeyse imkânsızdır.

Eşik aynı zamanda `NodeDropped`'ın ne zaman atılacağını belirliyor: figür gerçekten hareket ettiyse kaydediyoruz, sadece tıklandıysa hayır.

### 2.4 `CapturePointer` neden şart?

Fareyi hızlı hareket ettirdiğinde imleç figürün dışına çıkar. Yakalama olmadan `PointerMoved` olayları o anda başka bir elemana gitmeye başlar ve sürükleme yarıda kopar.

`CapturePointer` ile olaylar, bırakılana kadar o elemana yönlendirilir. `PointerCaptureLost` da ayrıca dinleniyor — pencere odağı kaybederse sürüklemenin temiz bitmesi için.

### 2.5 Sınırlar neden sürükleme sırasında da hesaplanıyor?

Faz 5'te sınırlar üyelerin konumundan türetiliyordu. Sürükleme o konumları değiştiriyor, dolayısıyla sınır da değişmeli — yoksa figür kendi mahallesinin dışına çıkar ve sınır yerinde kalır.

Bunun için motora ikinci bir metot ekledik:

```csharp
CityLayout Rebound(CityMap map, IReadOnlyDictionary<string, LayoutPoint> nodes, LayoutOptions? options = null);
```

`Arrange` konumlara **karar verir**, `Rebound` verilmiş konumlardan bölgeleri **yeniden hesaplar**. `Arrange` artık kendi işi bitince `Rebound`'u çağırıyor; tek bir sınır hesabı var, iki giriş noktası.

Her fare hareketinde 10 düğüm üzerinde çalışan bir hesap — ölçeklenmeyeceği gün gelirse fark edilir, bugün değil.

> **Overlay bayrağı nereden geliyor?** `Rebound`'un yerleştirme geçmişi yok. Bu yüzden "hangi district yeni servis getirdi" bilgisini haritadan türetiyoruz:
> ```csharp
> var claimed = district.Members.Count(member => seen.Add(member.Name));
> ```
> Konumlardan bağımsız, sadece `CityMap`'e bakıyor. Böylece ilk yerleşimde de, veritabanından geri yüklemede de aynı cevabı veriyor.

### 2.6 Kaydedilmiş konum hesaplananı ezer

```csharp
var positions = layout.Nodes.ToDictionary(
    pair => pair.Key,
    pair => stored.TryGetValue(pair.Key, out var saved)
        ? new LayoutPoint(saved.X, saved.Y)
        : pair.Value,
    StringComparer.Ordinal);
```

Dikkat: **hesaplanan düzen yine de üretiliyor.** Sadece kaydı olan servisler eziliyor. Dosyaya yeni bir servis eklediğinde onun kaydı olmaz ve otomatik yerleşimden gelir — diğerleri yerinde kalır.

Bu, "kaydedilmişse hepsini kullan, yoksa hiçbirini" yaklaşımından çok daha iyi davranır ve fazladan kod gerektirmez.

### 2.7 `Reset layout` düğmesi

`ILayoutStore`'a bir metot ekledik:

```csharp
Task<int> ClearAsync(int composeProjectId, CancellationToken cancellationToken = default);
```

Faz 3'te yazmamıştık çünkü ihtiyaç yoktu. Şimdi var: kullanıcı düzeni karıştırdıysa geri dönebilmeli. `ExecuteDeleteAsync` ile tek sorguda siliniyor — satırları çekip nesne olarak işaretleyip `SaveChanges` çağırmaya gerek yok.

Dönüş değeri silinen satır sayısı, bu da kullanıcıya dürüst bir geri bildirim veriyor: *"Cleared 3 stored positions"* ya da *"Layout was already the computed one."*

---

## 3. Adım adım uygulama

### Adım 1 — Motor: `Rebound`

`GridCityLayoutEngine` ikiye ayrılıyor. `Arrange` yalnızca yerleştirme yapıyor ve sonunda:

```csharp
return Rebound(map, nodes, settings);
```

Sınır hesabı, kapsam hesabı ve overlay bayrağı artık tek yerde.

> Bu refaktör de mevcut testleri kıpırdatmadı — `Known_geometry_is_stable`'daki 680×624 aynı. Faz 5'te de öyle olmuştu. **Aynı hesabı iki yoldan yapıyorsan, birini silip diğerine yönlendirdiğinde sonuç değişmemeli; değişiyorsa ikisi zaten aynı şey değildi.**

### Adım 2 — Veri: `ClearAsync`

```csharp
public async Task<int> ClearAsync(int composeProjectId, CancellationToken cancellationToken = default)
{
    return await context.ServiceLayouts
        .Where(layout => layout.ComposeProjectId == composeProjectId)
        .ExecuteDeleteAsync(cancellationToken);
}
```

Testi, silmenin **yalnızca o projeyi** etkilediğini doğruluyor — iki proje, üç konum, biri silinince diğeri duruyor.

### Adım 3 — `CityWorkspace`: yükleme artık veritabanına uğruyor

`Load` → `LoadAsync` oldu ve dört adım yapıyor: parse, harita, proje kaydı, kaydedilmiş konumlar.

Dosya özeti de burada hesaplanıyor:

```csharp
private static async Task<string> HashAsync(string path, CancellationToken cancellationToken)
{
    await using var stream = File.OpenRead(path);

    return Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken));
}
```

Bugün kullanılmıyor. Faz 9'da "bu dosya en son açtığından beri değişmiş" diyebilmek için `ComposeProjects.FileHash` kolonu Faz 3'ten beri orada duruyordu; doldurmanın maliyeti bir satır.

### Adım 4 — Seçim

`ServiceNodeViewModel`'de:

```csharp
[ObservableProperty]
[NotifyPropertyChangedFor(nameof(SelectionOpacity))]
private bool _isSelected;

public double SelectionOpacity => IsSelected ? 1 : 0;
```

`[NotifyPropertyChangedFor]`, `IsSelected` değiştiğinde `SelectionOpacity` için de bildirim üretiyor. Elle yazsaydın iki `OnPropertyChanged` çağrısı gerekirdi ve birini unutmak kolaydı.

`Visibility` yerine `Opacity` kullanmak bir değer dönüştürücüden kurtarıyor. Ek faydası: halka her zaman görsel ağaçta, seçim yerleşimi kaydırmıyor.

`MainViewModel.Select` eski seçimi temizliyor:

```csharp
if (Selected is not null) Selected.IsSelected = false;
Selected = node;
if (node is not null) node.IsSelected = true;
```

### Adım 5 — Sürükleme

Dört olay: `PointerPressed`, `PointerMoved`, `PointerReleased`, `PointerCaptureLost`. Üç olay dışarı veriliyor:

```csharp
public event EventHandler<ServiceNodeViewModel>? NodeSelected;  // basışta
public event EventHandler<ServiceNodeViewModel>? NodeMoving;    // sürüklerken, sürekli
public event EventHandler<ServiceNodeViewModel>? NodeDropped;   // bırakışta, hareket ettiyse
```

`NodeMoving` sınırları güncelliyor (ucuz, senkron), `NodeDropped` veritabanına yazıyor (pahalı, asenkron). Ayrı olmaları önemli: her fare hareketinde disk yazmak istemeyiz.

Koordinatlar canvas'a göre alınıyor:

```csharp
var position = args.GetCurrentPoint(this).Position;
```

`this` burada `CityCanvas`. Figüre göre alsaydın, figür hareket ettikçe referans da hareket eder ve sürükleme kendini kovalardı.

### Adım 6 — Detay paneli

Panel doğrudan `Selected.*` üzerinden bağlanıyor:

```xml
<TextBlock Text="{Binding Selected.PortsText}" TextWrapping="Wrap" />
```

`Selected` null ise bu bağlamalar sessizce boş dönüyor — görünürlük mantığı, dönüştürücü, hiçbiri gerekmiyor. Başlık `SelectionTitle` ise null durumunda *"No service selected"* diyor.

Liste alanları ViewModel'de biçimlendirilmiş metne çevriliyor. `ItemsControl` + `DataTemplate` daha "doğru" olurdu ama sekiz alan için sekiz şablon, karşılığında hiçbir kazanç yok.

Ortam değişkenleri Faz 2'de yazdığımız maskelemeyi kullanıyor:

```csharp
string.Join("\\n", _service.Environment.Select(entry => $"{entry.Key} = {entry.DisplayValue}"))
```

`POSTGRES_PASSWORD` satırında değer değil `*******` görünüyor. Dashboard'un ekran paylaşımında açılacağını varsaymak makul.

### Adım 7 — Boş zemine tıklama

```csharp
CityBoard.PointerPressed += (_, _) => _viewModel.Select(null);
```

Figüre basıldığında `args.Handled = true` yapıldığı için olay canvas'a **çıkmıyor**. Zemine basıldığında ise çıkıyor ve seçim temizleniyor. Faz 5'te `DistrictControl`'e koyduğumuz `IsHitTestVisible="False"` de burada işe yarıyor: bölge dikdörtgeni araya girmiyor.

---

## 4. Takıldığın yerler

**Sürükleme figürün ortasından değil, köşesinden tutuyor gibi davranıyor.**
Delta hesabı doğru ama başlangıç noktası yanlış alınmış olabilir. `_dragStart` canvas'a göre, `_originX/_originY` düğümün o anki konumu olmalı.

**Sürükleme fareyi hızlandırınca kopuyor.**
`CapturePointer` çağrılmamış.

**Her tıklama "Saved position of ..." yazıyor.**
`_dragMoved` bayrağı hiç sıfırlanmıyor ya da eşik kontrolü atlanmış.

**Mahalle sınırı sürüklerken yerinde kalıyor.**
`NodeMoving` olayı bağlanmamış ya da `MainViewModel.NodeMoved` `_map` null olduğu için erken dönüyor.

**Figürü taşıdım ama yeniden açınca eski yerinde.**
Üç yerde kopmuş olabilir: `NodeDropped` bağlanmamış, `_projectId` sıfır kalmış, ya da `LoadAsync` kaydedilmiş konumları okumuyor. Durum çubuğunda "Saved position of ..." görüyor musun?

**Sağ panel hep boş.**
`Selected` atanıyor mu? `CityBoard.NodeSelected` bağlanmış olmalı. Ayrıca `RootGrid.DataContext` atanmadıysa hiçbir bağlama çalışmaz.

**Boş zemine tıklayınca seçim kalkmıyor.**
Figürün `PointerPressed` işleyicisinde `args.Handled = true` var mı? Yoksa olay yukarı çıkar ve seçim hemen temizlenir — yani tam tersi belirti de aynı satırdan gelir.

**`ExecuteDeleteAsync` bulunamıyor.**
`Microsoft.EntityFrameworkCore` using'i eksik. EF Core 7'den beri var.

---

## 5. Ne öğrendik

- **Bir özelliği planlandığı gibi değil, doğru yerde yapmak da bir karardır.** Zengin detay balona değil panele taşındı; özellik düşmedi, maliyeti düştü.
- **Etkileşimde eşik değerleri isteğe bağlı değildir.** Dört piksel olmadan her tıklama bir yazma işlemi olur.
- **Pointer yakalama, sürüklemenin şartıdır.** Yakalamadan yapılan sürükleme, fare hızlandığında kopar.
- **Ucuz ve pahalı işlemleri ayrı olaylara bağla.** `NodeMoving` her harekette, `NodeDropped` bir kez.
- **Kısmi geri yükleme tam geri yüklemeden iyidir.** Kaydı olan servisler kaydından, olmayanlar hesaptan gelir; dosyaya yeni servis eklendiğinde bu fark kendini gösterir.
- **Aynı hesabı tek yere indirmek doğru refaktördür.** `Arrange` artık `Rebound`'u çağırıyor ve çivilenmiş koordinatlar değişmedi.
- **`[NotifyPropertyChangedFor]` unutulan bildirimleri önler.** Türetilmiş özellikleri elle haber vermek, sessiz arayüz hatalarının klasik kaynağıdır.

---

## 6. MVP kabul kriterleri

Faz 0'da koyduğumuz liste:

- [x] Örnek dosya hatasız parse ediliyor — 10 servis, 4 volume, 1 açık + 1 örtük network
- [x] Her servis doğru kategoriye ve ikona eşleşiyor; eşleşmeyen `GenericService` olarak düşüyor
- [x] `fnp-network` üyeleri bir bölge içinde, `keycloak` ve `minio` ayrı kenar mahallede
- [x] Hover'da balon açılıyor; image, container adı, portlar, volume'ler ve network'ler görünüyor
- [x] Figürler sürüklenebiliyor, konum kaydediliyor ve yeniden açılışta geri geliyor
- [x] Parser ve domain katmanı için testler yeşil
- [x] Faz 0–6 dokümanları yazılmış

**MVP tamam.**

---

## 7. Kendin dene

1. **İki dosya, iki düzen.** Fixture'ın bir kopyasını başka bir klasöre koy, ikisini de aç, ikisinde de figürleri farklı yerlere taşı. Sonra sırayla tekrar aç. Düzenler karışıyor mu? `ServiceLayouts` tablosuna bak — neden karışmıyor?

2. **Yeni servis ekle.** Konumları taşıdıktan sonra compose dosyasına yeni bir servis ekle ve tekrar aç. Yeni servis nereye düştü, eskiler yerinde mi? Bölüm 2.6'daki kararın karşılığını gör.

3. **Eşiği kaldır.** `DragThreshold`'u 0 yap ve birkaç kez tıkla. Durum çubuğunda ne oluyor? `ServiceLayouts` tablosunda kaç satır var?

4. **Yakalamayı kaldır.** `CapturePointer` satırını sil ve figürü hızlıca sürükle. Ne oluyor? Bu hatayı bir kullanıcı sana nasıl tarif ederdi?

5. **Sınırı dondur.** `NodeMoving` aboneliğini kaldır, sadece `NodeDropped` kalsın. Sürükleme sırasındaki his nasıl değişiyor? Hangi versiyon daha doğru, neden?

6. **Kendi ölçütünü koy.** MVP bitti. Şimdi kendi kullanımında eksik bulduğun üç şeyi yaz. Faz 7–10'un planı bunlarla çakışıyor mu, yoksa planı güncellemek mi gerekiyor?

---

**Sonraki bölüm:** `07-baglantilar.md` — `depends_on` okları. `pgadmin → postgres` ilişkisi Faz 2'den beri `CityMap.Links` içinde duruyor ve hiç çizilmedi. Bezier eğrisi, ok başı, ve sürükleme sırasında canlı güncellenen geometri. Z-sırası da orada üçüncü katmanını kazanacak: bölgeler, **oklar**, figürler.
