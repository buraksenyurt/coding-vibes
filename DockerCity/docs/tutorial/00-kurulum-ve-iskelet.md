# Faz 0 — Atölye Kurulumu ve Solution İskeleti

> **Seri:** DockerCity — docker-compose topoloji dashboard'u
> **Önceki bölüm:** yok · **Sonraki bölüm:** `01-yaml-parser.md`
> **Tahmini süre:** 45–60 dakika (kurulumlar hazırsa 20 dakika)

---

## 1. Bu bölümde ne yapacağız

Tek satır kod yazmadan önce ortamı kuruyoruz. Sonunda elimizde şu olacak:

- 6 projeli, derlenen bir solution
- Açılan boş bir WinUI 3 penceresi
- Katmanlar arası bağımlılık yönü derleyici tarafından garanti altına alınmış bir yapı

Kulağa sıkıcı geliyor olabilir ama Faz 0'da verilen iki karar (hedef framework ayrımı ve deployment modeli) sonraki dokuz fazın tamamını etkiliyor. Bu yüzden hızlı geçmek yerine "neden" kısmını okumanı öneririm.

---

## 2. Neden böyle

### 2.1 Neden 4 ayrı proje? Tek projede yapılamaz mı?

Yapılır. Küçük bir araç için tek proje tamamen makul. Ama bu bir **atölye** projesi ve burada öğrenmek istediğimiz şeylerden biri, bağımlılık yönünün nasıl mimari bir araç haline geldiği.

Tek projede `Domain` sınıfın kazara bir `Microsoft.UI.Xaml` tipine dokunduğunda hiçbir şey seni uyarmaz. Ayrı projede ise **derleme hatası** alırsın. Kural, yorum satırında değil, proje dosyasında yaşar.

### 2.2 Neden çekirdek katmanlar `net10.0`, sadece App `net10.0-windows…`?

Bu, yukarıdaki fikrin en keskin hali. `Domain`, `Parsing` ve `Data` projelerini **platformdan bağımsız** `net10.0` olarak hedefliyoruz. Bunun üç getirisi var:

1. **Sızıntı imkânsız.** `net10.0` hedefleyen bir projede `Microsoft.UI.Xaml` referansı zaten çözülmez. UI'ın iş mantığına sızması derleme zamanında engellenir.
2. **Testler hızlı.** Test projeleri Windows App SDK yüklemeden, düz .NET olarak koşar.
3. **Taşınabilirlik.** İleride "bunun bir CLI hali olsa" ya da "web'de göstersek" dersen, çekirdek katman olduğu gibi taşınır.

Windows hedefli bir proje, platformdan bağımsız bir projeye referans verebilir — tersi olmaz. Bağımlılık yönü tam da istediğimiz tarafa akıyor.

### 2.3 Packaged (MSIX) mi, unpackaged mı?

WinUI 3 masaüstü uygulaması iki şekilde çalışabilir:

| | Packaged (MSIX) | Unpackaged |
|---|---|---|
| Çıktı | MSIX paketi | Düz `.exe` |
| Kurulum | Paket yüklenir | Klasörü kopyala, çalıştır |
| Veri yolu | İzole paket depolaması | Normal `%LOCALAPPDATA%` |
| Dağıtım | Sertifika ister | Sertifika istemez |
| Store | Gerekli | Mümkün değil |

Biz **unpackaged** ile gidiyoruz. Gerekçe: bu kişisel bir araç, Store'a çıkmayacak; SQLite dosyasının normal `%LOCALAPPDATA%` altında, Explorer'dan bakabileceğin bir yerde durması geliştirme sırasında işini kolaylaştıracak; sertifika/paket kimliği uğraşı yok.

> **Yaygın yanlış bilgi:** "Unpackaged'da `FileOpenPicker` çalışmaz." Doğru değil. WinUI 3 masaüstünde `FileOpenPicker` **her iki modda da** pencere handle'ı ister (`InitializeWithWindow`). Bu bir packaged/unpackaged farkı değil, WinUI 3 masaüstünün genel davranışı. Faz 4'te karşımıza çıkacak.

Unpackaged'da bir şey ters giderse geri dönüş tek satır: `csproj`'dan `<WindowsPackageType>None</WindowsPackageType>` satırını silmek. Kilitlenmiş bir karar değil.

---

## 3. Adım adım uygulama

### Adım 1 — Ön koşulları doğrula

PowerShell aç:

```powershell
dotnet --list-sdks
```

Listede `10.x.x` görmelisin. Görmüyorsan .NET 10 SDK'yı kur.

Ardından **Visual Studio Installer**'ı aç ve şu workload'un kurulu olduğundan emin ol:

- **Windows uygulama geliştirme** (*Windows application development*)

Bu workload, WinUI 3 / Windows App SDK proje şablonlarını getirir. Yoksa "Değiştir" (*Modify*) ile ekle. `.NET masaüstü geliştirme` workload'u **tek başına yetmez** — WinUI 3 şablonları o pakette değil.

> Visual Studio kullanmıyorsan WinUI 3 projesini oluşturmak için en pratik yol yine de VS'tir; geri kalan her şeyi (sınıf kütüphaneleri, testler, referanslar) `dotnet` CLI ile yapacağız.

---

### Adım 2 — Klasör ve solution

```powershell
cd C:\Users\burak\Development\coding-vibes\DockerCity
dotnet new sln -n DockerCity
```

`dockercity` klasörü ve alt klasörleri (`docs`, `samples`, `assets`) zaten hazır durumda.

---

### Adım 3 — Çekirdek kütüphaneler

```powershell
dotnet new classlib -o src\DockerCity.Domain  -f net10.0
dotnet new classlib -o src\DockerCity.Parsing -f net10.0
dotnet new classlib -o src\DockerCity.Data    -f net10.0
```

Şablonun ürettiği `Class1.cs` dosyalarını üçünden de sil — boş başlıyoruz.

```powershell
del src\DockerCity.Domain\Class1.cs
del src\DockerCity.Parsing\Class1.cs
del src\DockerCity.Data\Class1.cs
```

---

### Adım 4 — Test projeleri

```powershell
dotnet new xunit -o tests\DockerCity.Domain.Tests  -f net10.0
dotnet new xunit -o tests\DockerCity.Parsing.Tests -f net10.0
```

---

### Adım 5 — Projeleri solution'a ekle

```powershell
dotnet sln add src\DockerCity.Domain\DockerCity.Domain.csproj
dotnet sln add src\DockerCity.Parsing\DockerCity.Parsing.csproj
dotnet sln add src\DockerCity.Data\DockerCity.Data.csproj
dotnet sln add tests\DockerCity.Domain.Tests\DockerCity.Domain.Tests.csproj
dotnet sln add tests\DockerCity.Parsing.Tests\DockerCity.Parsing.Tests.csproj
```

---

### Adım 6 — Bağımlılık yönünü kur

```powershell
dotnet add src\DockerCity.Parsing reference src\DockerCity.Domain
dotnet add src\DockerCity.Data    reference src\DockerCity.Domain
dotnet add tests\DockerCity.Domain.Tests  reference src\DockerCity.Domain
dotnet add tests\DockerCity.Parsing.Tests reference src\DockerCity.Parsing
```

Dikkat: `Domain` hiç kimseye referans vermiyor. Bu bilinçli. Sonraki fazlarda "şunu Domain'e koysam mı?" diye düşündüğünde ölçütün bu olsun — **Domain'e bir referans eklemek zorunda kalıyorsan, muhtemelen o kod Domain'e ait değil.**

---

### Adım 7 — `Directory.Build.props`

Solution kökünde (`dockercity\Directory.Build.props`) bir dosya oluştur:

```xml
<Project>
  <PropertyGroup>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <EnforceCodeStyleInBuild>true</EnforceCodeStyleInBuild>
    <GenerateDocumentationFile>false</GenerateDocumentationFile>
  </PropertyGroup>
</Project>
```

MSBuild, her projeyi derlerken klasör ağacında yukarı doğru `Directory.Build.props` arar ve bulduğunu `csproj`'dan **önce** uygular. Yani bu dosya, altındaki tüm projeler için ortak varsayılan haline gelir. Tek bir yerde `Nullable` açtık, altı projede birden açıldı.

`<Nullable>enable</Nullable>` özellikle önemli: `container_name` gibi compose alanlarının çoğu opsiyonel. Derleyicinin "burası null olabilir" diye bağırması, Faz 2'de domain modelini tasarlarken en iyi yardımcın olacak.

---

### Adım 8 — WinUI 3 App projesi

Bunu Visual Studio'dan yapıyoruz (CLI şablonu VS workload'una göre değişkenlik gösterebiliyor).

1. Visual Studio'da `DockerCity.sln`'i aç
2. Solution'a sağ tık → **Ekle → Yeni Proje**
3. Şablon ara: **Blank App, Packaged (WinUI 3 in Desktop)** — C#
4. Proje adı: `DockerCity.App`
5. Konum: solution'ın `src` klasörü → yol `src\DockerCity.App` olmalı
6. Hedef/minimum Windows sürümü sorulursa varsayılanları kabul et

Proje oluştuktan sonra `src\DockerCity.App\DockerCity.App.csproj` dosyasını aç ve ilk `<PropertyGroup>` içine unpackaged geçişi için şu satırı ekle:

```xml
<WindowsPackageType>None</WindowsPackageType>
```

Ardından `TargetFramework` satırına bak. Şablon büyük ihtimalle `net8.0-windows10.0.19041.0` gibi bir değer üretmiş olacak. Bunu .NET 10'a çekiyoruz:

```xml
<TargetFramework>net10.0-windows10.0.26100.0</TargetFramework>
```

> ⚠️ **Buradaki tek tuzak, serinin en çok zaman kaybettiren noktası.** `-windows10.0.XXXXX.0` eki **zorunlu**. `net8.0-windows10.0.19041.0` değerini `net10.0` yapıp eki düşürürsen:
> - `dotnet restore` **başarılı olur** (paketler `net10.0` ile uyumludur),
> - ama derleme, XAML derleyicisi aşamasında patlar: `Microsoft.UI.Xaml` çözülmez, `bin` klasörü boş kalır.
>
> Restore'un geçmesi seni yanıltmasın. `Microsoft.UI.Xaml` ve `Windows.*` projeksiyonlarını projeye getiren şey NuGet paketi değil, **TFM'nin Windows platform eki**. Eksiz `net10.0` düz bir .NET uygulamasıdır — Windows Runtime'ı görmez.

Sayı kısmı (`26100`) hedeflediğin Windows SDK sürümü; .NET SDK bu targeting pack'i gerekirse otomatik indirir. `26100` ile sorun yaşarsan şablonun ürettiği sürümü koruyup sadece .NET tarafını değiştir: `net10.0-windows10.0.19041.0`.

Bir de şu satırı düzelt — şablon bazen geçersiz bir değer bırakıyor:

```xml
<TargetPlatformMinVersion>10.0.17763.0</TargetPlatformMinVersion>
```

`TargetPlatformMinVersion` dört parçalı bir sürüm olmalı; `10.0.0` gibi bir değer bazı MSBuild adımlarında hataya yol açar.

Son olarak App'in referanslarını bağla:

```powershell
dotnet add src\DockerCity.App reference src\DockerCity.Domain
dotnet add src\DockerCity.App reference src\DockerCity.Parsing
dotnet add src\DockerCity.App reference src\DockerCity.Data
```

---

### Adım 9 — Pencereyi DockerCity'ye çevir

`MainWindow.xaml` içeriğini şununla değiştir:

```xml
<?xml version="1.0" encoding="utf-8"?>
<Window
    x:Class="DockerCity.App.MainWindow"
    xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
    mc:Ignorable="d"
    xmlns:d="http://schemas.microsoft.com/expression/blend/2008"
    xmlns:mc="http://schemas.openxmlformats.org/markup-compatibility/2006">

    <Grid Background="{ThemeResource ApplicationPageBackgroundThemeBrush}">
        <StackPanel HorizontalAlignment="Center"
                    VerticalAlignment="Center"
                    Spacing="8">
            <TextBlock Text="DockerCity"
                       Style="{ThemeResource TitleLargeTextBlockStyle}"
                       HorizontalAlignment="Center" />
            <TextBlock Text="Şehir henüz kurulmadı."
                       Style="{ThemeResource BodyTextBlockStyle}"
                       Opacity="0.6"
                       HorizontalAlignment="Center" />
        </StackPanel>
    </Grid>
</Window>
```

`MainWindow.xaml.cs` içinde constructor'a pencere başlığını ekle:

```csharp
public MainWindow()
{
    InitializeComponent();
    Title = "DockerCity";
}
```

---

### Adım 10 — Doğrula

```powershell
dotnet build
dotnet test
```

`dotnet build` temiz geçmeli. `dotnet test` iki test projesini bulup "0 test" ile yeşil dönmeli (henüz test yok).

Sonra Visual Studio'da `DockerCity.App`'i başlangıç projesi yapıp **F5**. Ortada "DockerCity / Şehir henüz kurulmadı." yazan bir pencere açılmalı.

---

### Adım 11 — Commit

`coding-vibes` reposunun `.gitignore`'u `bin/`, `obj/`, `.vs/` klasörlerini zaten dışarıda tutuyor.

```powershell
cd C:\Users\burak\Development\coding-vibes
git add DockerCity
git status
```

`git status` çıktısında `bin` veya `obj` klasörü görüyorsan **commit etme**, önce `.gitignore`'u kontrol et.

```powershell
git commit -m "DockerCity: Faz 0 - solution iskeleti ve bos WinUI 3 penceresi"
```

---

## 4. Takıldığın yerler

**"Blank App, Packaged (WinUI 3 in Desktop)" şablonunu bulamıyorum.**
Visual Studio Installer → Değiştir → **Windows uygulama geliştirme** workload'u kurulu mu? Kurduktan sonra VS'i kapatıp açman gerekebilir.

**F5'te "The application is not packaged" / bootstrapper hatası alıyorum.**
`<WindowsPackageType>None</WindowsPackageType>` satırının doğru `<PropertyGroup>` içinde ve App projesinde olduğunu doğrula. Değişiklikten sonra `obj` ve `bin` klasörlerini silip yeniden derle — WinUI'de eski MSBuild çıktısı sık sık inatçılık eder.

**`restore` geçiyor ama App projesi derlenmiyor; `bin` boş kalıyor.**
Neredeyse kesinlikle `TargetFramework`'ten `-windows10.0.XXXXX.0` eki düşmüştür. Adım 8'deki uyarıya dön. Düzelttikten sonra **mutlaka** tüm `bin` ve `obj` klasörlerini sil — WinUI'nin XAML derleyicisi TFM değişince eski ara çıktılarla çakışır ve kafa karıştırıcı hatalar üretir:

```powershell
Get-ChildItem -Path . -Include bin,obj -Recurse -Directory | Remove-Item -Recurse -Force
```

`obj\x64\Debug\` altında birden fazla framework klasörü (`net8.0-windows…`, `net10`, `net10.0`) duruyorsa TFM'i birkaç kez değiştirmişsin demektir; temizlik şart.

**`error MSB4126: The specified solution configuration "Debug|x64" is invalid`**
`-p:Platform=x64`'ü **solution seviyesinde** vermişsin. App projesi `x64`, sınıf kütüphaneleri `AnyCPU` olduğu için eşleme tutmaz. İki doğru kullanım var:

```powershell
dotnet build                                                      # solution, platform bayragi yok
dotnet build src\DockerCity.App\DockerCity.App.csproj -p:Platform=x64   # tek proje
```

Hata ayıklarken ikincisini tercih et — solution konfigürasyon eşlemesini atlar, asıl derleme hatalarını gizlemez.

**Test projeleri `net10.0` ile oluşmadı.**
xUnit şablonu bazen SDK'nın varsayılan sürümünü kullanır. `csproj`'daki `TargetFramework` değerini elle `net10.0` yap.

**`Domain` projesine bir şey eklemek isteyince referans gerekiyor.**
Bu bir hata değil, tasarımın kendini savunması. O kodun gerçekten Domain'e mi ait olduğunu bir daha düşün.

---

## 5. Ne öğrendik

- **Bağımlılık yönü bir mimari araçtır.** Yorum satırıyla değil, proje referanslarıyla uygulanır.
- **Hedef framework de bir kısıttır.** `net10.0` seçerek UI tiplerinin çekirdek katmana sızmasını derleme zamanında imkânsız hale getirdik.
- **`Directory.Build.props`**, çok projeli solution'larda ortak ayarları tek noktadan yönetmenin yolu. MSBuild'in klasör ağacında yukarı doğru arama davranışına dayanır.
- **WinUI 3'te packaged/unpackaged** bir deployment kararıdır, yetenek kararı değil — ve tek satırla geri alınabilir.
- **Restore'un geçmesi, projenin doğru yapılandırıldığı anlamına gelmez.** NuGet uyumluluğu ile derleyicinin gördüğü API yüzeyi iki ayrı şey. WinUI'de bu ikisini birbirine bağlayan şey TFM'nin platform ekidir.
- WinUI 3 masaüstünde `Window` bir XAML kökü değildir; içine bir `Grid`/`Page` koyarsın. WPF'ten gelen alışkanlıkların birebir geçmediği ilk nokta.

---

## 6. Kendin dene

1. **Bağımlılık kuralını test et.** `DockerCity.Domain` içine `using Microsoft.UI.Xaml;` yazan bir sınıf ekle ve derle. Aldığın hatayı oku — bu, mimarinin kendini savunduğu andır. Sonra sil.

2. **`Directory.Build.props`'u kurcala.** Dosyaya `<TreatWarningsAsErrors>true</TreatWarningsAsErrors>` ekle, derle, ne olduğuna bak. Sonra karar ver: bu projede istiyor musun? *(Öneri: Faz 1'e kadar kapalı tut, YamlDotNet ile uğraşırken uyarı gürültüsü olacak.)*

3. **`.slnx` formatını araştır.** .NET 10 ile gelen yeni, XML tabanlı solution formatı. Mevcut `.sln`'i dönüştürmenin ne kazandırdığına bak — özellikle merge conflict açısından.

4. **Pencere boyutunu ayarla.** WinUI 3'te `MainWindow` constructor'ında pencere boyutunu nasıl belirlersin? (İpucu: `AppWindow` ve `Resize`.) Faz 4'te lazım olacak.

---

**Sonraki bölüm:** `01-yaml-parser.md` — YamlDotNet ile `samples/docker-compose.yml`'ı okuyup, o dosyadaki yedi kenar durumun her biriyle tek tek boğuşacağız.
