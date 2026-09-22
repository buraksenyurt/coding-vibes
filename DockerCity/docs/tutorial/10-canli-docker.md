# Faz 10 — Canlı Docker bağlantısı

> **Seri:** DockerCity — docker-compose topoloji dashboard'u
> **Önceki bölüm:** `09-zoom-pan-animasyon.md` · **Serinin son bölümü**
> **Tahmini süre:** 4–5 saat
> **Kod reposu:** `C:\Users\burak\Development\coding-vibes\DockerCity`

---

## 1. Bu bölümde ne yapacağız

Şehir şimdiye kadar bir **dosyanın resmiydi**. Compose dosyasında ne yazıyorsa onu gösteriyordu; o servislerin gerçekten ayakta olup olmadığından haberi yoktu.

Bu bölümde Docker Engine'e bağlanıyoruz:

| Özellik | Nerede |
|---|---|
| Docker'a bağlan, container'ları periyodik oku | `DockerCity.Live` (yeni proje) |
| Hangi container hangi servise ait? | `RuntimeMatcher` (Domain, testli) |
| Figürün ikonunda canlı ışık: yeşil / kırmızı / sarı / gri | `ServiceNodeControl` |
| Çalışan servis için yavaş nabız animasyonu | `ServiceNodeControl` |
| Durum çubuğunda `Docker: 7/10 running` | `MainViewModel.RuntimeText` |
| Açma/kapama ve hatırlama | View › Live status, Ctrl+5 |

**Kapsam kararı: sadece okuyoruz.** Başlat, durdur, yeniden başlat, log akışı yok. Gerekçe bir sonraki başlıkta.

---

## 2. Neden böyle

### 2.1 Neden sadece okumak?

Docker.DotNet ile `StartContainerAsync` çağırmak, `ListContainersAsync` çağırmaktan zor değil. Zor olan, bir masaüstü aracının kullanıcının ortamına müdahale etmesiyle başlayan her şey:

- Yanlış container'ı durdurmak. Eşleştirme kuralları (2.4) olasılıklıdır; okurken hatalı eşleşme yanlış bir ışık demektir, yazarken yanlış servisi durdurmak demek.
- Onay akışı, geri alma, "durdurma sırasında hata oldu" durumları, uzun süren işlemler için iptal.
- Yetki: Docker soketine erişimi olan bir uygulama, makinede yapılabilecek her şeyi yapabilir.

Okumak, aracın değerinin büyük kısmını **risksiz** veriyor: "dosyada 10 servis var, 7'si ayakta, biri sağlıksız". Komutlar isteniyorsa ayrı bir faz olur; o zaman onay ve hata yönetimi de o fazın işi olur, bu fazın yamalı eklentisi değil.

### 2.2 Yeni bir proje: `DockerCity.Live`

Docker.DotNet paketi tek bir yere giriyor:

```
DockerCity.Domain   ←  DockerCity.Live  (Docker.DotNet)
      ↑                      ↑
DockerCity.Data         DockerCity.App
```

Domain, Docker diye bir şey olduğunu bilmiyor. Orada duran tek şey bir arayüz:

```csharp
public interface IContainerProbe : IDisposable
{
    Task<IReadOnlyList<ContainerSnapshot>> ListAsync(CancellationToken cancellationToken = default);
}
```

Bunun üç faydası var. Birincisi, eşleştirme ve yorumlama mantığı Docker olmadan test edilebiliyor. İkincisi, `Docker.DotNet` tek bir dosyada; paket değişirse ya da Podman desteği eklenirse dokunulacak yer belli. Üçüncüsü, `RuntimeMonitor` bir fabrika alıyor (`Func<IContainerProbe>`), yani ileride "başka bir makinedeki Docker'a bağlan" özelliği uygulamanın geri kalanına dokunmadan yazılabilir.

Bağımlılık okunun yönü kritik: `Live` → `Domain`. Tersi olsaydı, en saf katmanımız bir HTTP istemcisine bağımlı hale gelirdi.

### 2.3 Docker'a nasıl bağlanılıyor?

Docker Engine bir HTTP API sunuyor ama TCP portu üzerinden değil:

```csharp
public static Uri LocalEndpoint() => OperatingSystem.IsWindows()
    ? new Uri("npipe://./pipe/docker_engine")
    : new Uri("unix:///var/run/docker.sock");
```

Windows'ta **adlandırılmış boru (named pipe)**, diğerlerinde **Unix soketi**. İkisi de yereldir. Bu, uygulamanın ancak Docker'ın kurulu olduğu makinede canlı durum gösterebileceği anlamına geliyor — istenen de bu. Uzaktaki bir daemon'a bağlanmak TLS sertifikaları ve `DOCKER_HOST` ayarları demek; bunu istemeyeceğimizi baştan söylemek, sonradan yarım yapmaktan iyi.

Docker.DotNet'in `DockerClient`'ı bir HTTP istemcisi gibi davranıyor: pahalı, uzun ömürlü olmalı, her çağrıda yeniden kurulmamalı. Bu yüzden `RuntimeMonitor` onu bir kez açıp saklıyor — ama bir hata olduğunda **atıyor**. Bozuk bir bağlantıyı tutmak, Docker geri geldiğinde hâlâ "bağlanamıyorum" demeye devam etmek demek olurdu.

### 2.4 Hangi container hangi servis?

Bu fazın asıl problemi. `docker-compose.yml`'de `redis` diye bir servis var; makinede çalışan onlarca container arasında hangisi o?

Tek bir doğru cevap yok, çünkü container şunlardan biriyle başlamış olabilir:

- bu dosyayla, `docker compose up` ile,
- aynı dosyanın başka bir klasördeki kopyasıyla,
- `-p` ile başka bir proje adı verilerek,
- elle, `docker run --name cache-box` ile.

O yüzden kural değil, **kural merdiveni** var. `RuntimeMatcher` sırayla dener, ilk cevap veren kazanır:

| # | Kural | Neden bu sırada |
|---|---|---|
| 1 | `com.docker.compose.project.config_files` etiketi, açık olan dosyanın tam yolunu içeriyor mu? | En kesin kanıt: container tam olarak bu dosyadan doğmuş |
| 2 | `com.docker.compose.project` + `com.docker.compose.service` etiketleri tutuyor mu? | Proje adı doğruysa güvenilir |
| 3 | Compose dosyasındaki `container_name` ile birebir aynı isim | Dosya ismi dayattığı için Docker başka isim kullanamaz |
| 4 | `proje_servis_1` / `proje-servis-1` kalıbı | Etiketsiz, eski Compose ya da elle başlatılmış container'lar |

Kaçınılan bir şey de var: **proje adı olmadan yalnız servis etiketine bakmak.** Hemen her projede `postgres` adında bir servis vardır; o eşleşme yanlış bilgiyi "bilgi" gibi gösterirdi. Bilmiyorsan ışık yakmamak, yanlış ışık yakmaktan iyidir.

Proje adı için Compose'un kuralını taklit etmek gerekiyor: dosyanın bulunduğu klasörün adı, küçük harfe çevrilmiş, `[a-z0-9_-]` dışındaki karakterler atılmış hali. `My Compose_1` → `mycompose_1`. Bu da testlik bir kural (`ComposeProjectName`).

Bir servisin birden fazla eşleşmesi olabilir: dünkü çıkmış container ile bugünkü çalışan. Ayakta olan kazanıyor.

### 2.5 Sağlık neden metinden okunuyor?

Docker'ın "list containers" cevabında `State` (`running`, `exited`...) var ama **health yok**. Health yalnızca `inspect` cevabında duruyor.

On servislik bir şehirde, üç saniyede bir on `inspect` çağrısı demek bu; yüz container'lı bir makinede daha fazlası. Oysa `Status` alanı zaten insan için yazılmış haliyle onu söylüyor:

```
Up 3 hours (healthy)
Up 2 minutes (unhealthy)
Up 5 seconds (health: starting)
```

`ContainerSnapshot.ParseHealth` bu metinden okuyor. Bu bir **kestirme** ve öyle olduğu biliniyor: Docker bir gün metni değiştirirse sağlık `None` görünür, yani en kötü ihtimalle bilgi kaybolur, yanlış bilgi üretilmez. Bu tip kestirmeler için doğru soru "hilesiz mi" değil, "yanlış tarafa mı düşüyor" olmalı.

Metin okumanın bir maliyeti daha var: yerelleştirme. Docker CLI İngilizce döndüğü sürece sorun yok; bu varsayım da doküman ve testlerle kayıt altında.

### 2.6 Neden yoklama (polling)?

Docker'ın bir olay akışı var (`/events`): container başladığında, durduğunda haber veriyor. Daha zarif ama daha pahalı: uzun ömürlü bir bağlantı, kopunca yeniden bağlanma, kaçırılan olaylar için yine de periyodik tam okuma. Health değişimleri de ayrı bir olay türü.

Üç saniyede bir "hepsini listele" çağrısı, onlarca container için milisaniyeler süren bir yerel çağrı. Bu fazın ihtiyacına göre yoklama **daha basit ve yeterince iyi**. Ama kibar olması şartıyla:

- **Üst üste binmiyor.** Bir yoklama hâlâ havadayken zamanlayıcı yeniden ateşlerse tur atlanıyor. Yoksa yavaş bir daemon'da cevaplar sıraya girer ve eski cevap yeni cevabın üstüne yazabilir.
- **Zaman aşımı var.** Her çağrı 5 saniyelik bir `CancellationTokenSource` ile yapılıyor.
- **Geri çekiliyor.** Bağlanamadıysa aralık 3 saniyeden 15 saniyeye çıkıyor. Docker kapalıysa her üç saniyede bir kapıyı çalmak onu açtırmıyor.
- **Arkada koşmuyor.** Pencere aktif değilken (`Activated` olayı) monitör duruyor, şehir kapalıyken zaten hiç başlamıyor.

Zamanlayıcı yine `DispatcherQueueTimer`: tik UI iş parçacığında geliyor, `await` sonrası da oraya dönüyor, dolayısıyla view model güncellemesi için ayrıca `TryEnqueue` gerekmiyor. İş zaten IO; UI iş parçacığı bekleme boyunca boşta.

### 2.7 Durum + sağlık = tek ışık

Görünümün `ContainerState` ile `HealthState`'i her yerde ayrı ayrı yorumlamasını istemiyoruz. İkisi Domain'de tek bir `RuntimeSignal`'e katlanıyor:

| Sinyal | Ne zaman | Renk |
|---|---|---|
| `Healthy` / `Running` | Ayakta; healthcheck geçiyor ya da hiç yok | Yeşil |
| `Unhealthy` | Ayakta ama kendi kontrolünü geçemiyor | Kırmızı |
| `Starting` | Healthcheck henüz karar vermedi, ya da yeniden başlıyor | Sarı |
| `Paused` | Duraklatılmış | Mavi |
| `Stopped` | Var ama çalışmıyor (created / exited / dead) | Gri |
| `Unknown` | Eşleşen container yok | Işık yok |

Son satır önemli: eşleşme yoksa **ışık hiç çizilmiyor**. Gri bir nokta "duruyor" demek; boş köşe "bu isimde bir container görmedim" demek. İkisi farklı bilgiler.

`Unhealthy` yine de "ayakta" sayılıyor (`IsUp`). Sağlıksız bir servis çalışmıyor değildir, kendi kontrolünü geçemiyordur; `7/10 running` sayımından düşürmek onu görünmez yapardı — zaten kırmızı yanıyor.

### 2.8 Nabız, poll'a göre değil duruma göre

Çalışan ışık yavaşça soluyor: `Opacity` üzerinde `AutoReverse` ve `RepeatBehavior.Forever` ile bir `Storyboard`. Basit görünüyor ama bir tuzağı var: bu animasyonu her yoklamada yeniden başlatırsak, nabız üç saniyede bir baştan alır ve ritim tutmaz.

Bu yüzden `ServiceNodeControl` view model'in `IsPulsing` özelliğini dinliyor ve animasyonu yalnızca **değer değiştiğinde** başlatıp durduruyor. Poll'lar sessizce aynı cevabı getirdiği sürece animasyona dokunulmuyor.

`FillBehavior.Stop` yine önemli (Faz 9'daki yol animasyonu gibi): container durduğunda animasyon duruyor ve `Opacity` yerel değerine, yani tam görünürlüğe dönüyor. `HoldEnd` olsaydı ışık yarı saydam donup kalırdı.

Ve her dekoratif hareket gibi bu da `Motion.IsEnabled`'a soruyor.

### 2.9 Docker yoksa ne oluyor?

Hiçbir şey patlamıyor, hiçbir şey de gizlenmiyor. Bağlantı hatası bir **sonuç**:

```csharp
public sealed record RuntimeUpdate(bool IsConnected, IReadOnlyList<ContainerSnapshot> Containers, string? Error);
```

Bağlantı yoksa bütün ışıklar sönüyor ve durum çubuğu sebebi yazıyor. Son poll'ün renklerini ekranda bırakmak en kötü seçenek olurdu: kullanıcı yarım dakika önceki gerçeği şimdiki gerçek sanardı. **Eski bilgi, bilgi değildir.**

Bir ayrıntı daha: canlı durum kapatıldığında havada kalan bir yoklamanın cevabı geri döndüğünde ışıkları yeniden yakmamalı. `ApplyRuntime` ilk satırında bunu kontrol ediyor. Asenkron kodda "artık istenmeyen cevap" her zaman ayrı bir durumdur.

### 2.10 Görünümün bilmediği şeyler

Figür view model'i canlı durumu tek bir `ContainerSnapshot?` olarak tutuyor; `null` ise ışık yok. Renk seçimi bir converter'da (`SignalToBrushConverter`), çünkü renk bir görünüm kararı. Eşleştirme, durum yorumu ve sinyal Domain'de, çünkü onlar kural.

Bu ayrım sayesinde bu fazda **hiçbir UI testi yazmak gerekmedi**: yeni mantığın tamamı `dotnet test` ile koşan saf fonksiyonlarda.

---

## 3. Adım adım uygulama

### Adım 1 — Domain: çalışma zamanı tipleri

`src/DockerCity.Domain/Runtime/`:

- `ContainerState.cs`, `HealthState.cs`, `RuntimeSignal.cs` — sözlük.
- `ContainerSnapshot.cs` — bir container'ın anlık hali; `From(...)`, `ParseState`, `ParseHealth`, `Signal`, `IsUp`.
- `RuntimeMatcher.cs` — kural merdiveni.
- `ComposeProjectName.cs` — klasör adından proje adı.
- `IContainerProbe.cs` — uygulamanın Docker'dan tek beklentisi.

### Adım 2 — `DockerCity.Live`

Yeni bir `net10.0` kütüphanesi, `Docker.DotNet` 3.125.15 paketi ve Domain referansı. İçinde tek sınıf: `DockerContainerProbe`. `DockerCity.slnx`'e ve `DockerCity.App`'in referanslarına eklemeyi unutma.

```xml
<PackageReference Include="Docker.DotNet" Version="3.125.15" />
```

### Adım 3 — `RuntimeMonitor`

`src/DockerCity.App/Services/RuntimeMonitor.cs`: `DispatcherQueueTimer`, üst üste binmeyen yoklama, zaman aşımı, geri çekilme, `RuntimeUpdate` olayı.

### Adım 4 — ViewModel

- `MainViewModel`: `ShowLiveStatus` (tercih anahtarı `view.live`), `RuntimeText`, `ApplyRuntime(RuntimeUpdate)`, `ClearRuntime()`, yükleme sırasında `_composeProject`.
- `ServiceNodeViewModel`: `Runtime`, `HasRuntime`, `Signal`, `IsPulsing`, `LiveText`.

### Adım 5 — Görünüm

- `Converters/SignalToBrushConverter.cs`.
- `ServiceNodeControl.xaml`: ikonun sol altında `LiveDot`; `ServiceNodeControl.xaml.cs`: nabız.
- `MainWindow.xaml`: View › Live status (Ctrl+5), detay panelinde "Runtime" satırı, durum çubuğunda `RuntimeText`.
- `MainWindow.xaml.cs`: monitörün kurulumu, `Activated`, `UpdateRuntimeMonitor()`, kapanışta `Dispose`.

### Adım 6 — Çalıştır ve dene

```powershell
dotnet test
dotnet build src\DockerCity.App -p:Platform=x64
```

Kontrol listesi:

1. Docker Desktop açıkken compose dosyanı aç: ayakta olan servislerin ikonunda yeşil, nabız atan bir nokta.
2. `docker compose stop redis` — birkaç saniye içinde nokta griye dönüyor ve nabız duruyor.
3. `docker compose start redis` — yeşile dönüyor. Healthcheck'i olan bir servis önce sarı görünüyor mu?
4. Durum çubuğu `Docker: 7/10 running` gibi bir şey yazıyor mu?
5. Docker Desktop'ı kapat: ışıklar sönüyor, durum çubuğu sebebi yazıyor, uygulama donmuyor.
6. Ctrl+5 ile kapat-aç; uygulamayı kapatıp açınca tercih hatırlanıyor mu?
7. Projeyi `docker compose -p baskaad up -d` ile başlat: ışıklar hâlâ doğru mu? (1. kural devrede: `config_files`.)
8. Aynı compose dosyasının bir kopyasını başka klasöre koy, birini çalıştır, diğerini uygulamada aç: yanlış eşleşme oluyor mu?
9. Uygulamayı arka plana al, Görev Yöneticisi'nde CPU'ya bak: yoklama duruyor mu?
10. Bir container'ın üzerine gel: ipucu `dockercity-redis-1 · Up 3 hours (healthy)` gibi bir şey gösteriyor mu?

---

## 4. Takıldığın yerler

**"Docker'a bağlanılamıyor" ama Docker açık.** Windows'ta Docker Desktop'ın WSL arka ucu açılırken boru birkaç saniye gecikebilir. Monitör zaten 15 saniyede bir yeniden deniyor; ışıklar kendiliğinden gelmeli. Gelmiyorsa `docker context ls` ile aktif bağlam bakılır: uzak bir bağlama geçilmişse yerel boru boş olur.

**Bütün servisler gri.** Container'lar var ama eşleşme yok demektir. `docker inspect <container> --format "{{json .Config.Labels}}"` ile etiketlere bak: `com.docker.compose.project.config_files` açtığın dosyanın yolunu gösteriyor mu? Sembolik bağlantı ya da eşlenmiş sürücü üzerinden açılan dosyalar farklı tam yol üretir.

**Sağlık hiç görünmüyor.** Servisin `healthcheck`'i yoksa health de yoktur; yeşil, "sağlıklı" değil "ayakta" demektir.

**Uygulama kapanırken takılıyor.** Havada bir yoklama varken `Dispose` çağrılırsa `DockerClient` kapanır ve bekleyen çağrı hata verir; hata `PollAsync` içinde yakalanıyor. Kapanışta `Stop` + `Dispose` sırası bu yüzden önemli.

**Docker.DotNet ve .NET 10.** Paket `netstandard2.0` hedefliyor ve 2023'ten beri güncellenmedi; .NET 10 ile çalışıyor ama bir gün çalışmazsa alternatif, Engine API'sine doğrudan `HttpClient` ile gitmek olur — `IContainerProbe` arayüzü zaten o günü ucuzlatıyor.

---

## 5. Ne öğrendik

- **Kapsamı daraltmak da bir tasarım kararıdır.** Yalnız okuyan bir araç, değerin çoğunu riskin hiçbirini almadan verdi.
- **Bağımlılık okunun yönü mimaridir.** `Live → Domain`; Domain'in Docker'dan haberi yok, bu yüzden bütün kurallar testlenebilir kaldı.
- **Kesinlik yoksa kural merdiveni kur.** En güçlü kanıttan en zayıfına; ve zayıf kanıt yanlış bilgi üretecekse, bilgi vermemeyi seç.
- **Ucuz kestirme, yanlış tarafa düşmediği sürece meşrudur.** Health'i metinden okumak bilgi kaybedebilir, uydurmaz.
- **Yoklama kibar olmak zorundadır:** üst üste binmesin, zaman aşımı olsun, geri çekilsin, görünmeyen yerde koşmasın.
- **Eski bilgi bilgi değildir.** Bağlantı kopunca son bilinen renkleri ekranda bırakmak, kullanıcıyı yanıltmaktır.
- **Asenkron dünyada "artık istenmeyen cevap" ayrı bir durumdur.** Kapatıldıktan sonra dönen poll'u yok saymak tek satır, ama olmayınca hata.
- **Animasyonu veriye değil, verinin değişimine bağla.** Her yoklamada yeniden başlayan bir nabız, nabız değil titremedir.

---

## 6. Kendin dene

1. **Olay akışına geç.** `/events` akışını dinleyip yoklamayı yalnızca emniyet ağı olarak (30 saniyede bir) bırak. Bağlantı koptuğunda ne yapıyorsun? Kaçırılan olayları nasıl telafi edersin?

2. **Sahte bir prob yaz.** `IContainerProbe`'u uygulayan, senaryo döndüren bir sınıf (`FakeProbe`) yaz ve `RuntimeMonitor`'ü Docker olmadan test et: geri çekilme gerçekten oluyor mu, üst üste binme engelleniyor mu?

3. **CPU ve port bilgisi.** `docker stats` benzeri bir akışla figürün altına anlık CPU/bellek yaz. Hangi çağrı? Yoklama aralığını değiştirmen gerekir mi?

4. **Durduran servisi vurgula.** Bir servis `depends_on` ile bağlı olduğu servis çalışmıyorken ayaktaysa, o yolu kırmızı çiz. Kural nerede yaşamalı: `LinkViewModel`'de mi, Domain'de mi?

5. **Uzak daemon.** `DOCKER_HOST` ortam değişkenini okuyup oradaki daemon'a bağlan. `DockerContainerProbe`'da kaç satır değişiyor? Uygulamanın geri kalanında kaç satır?

6. **Yanlış eşleşmeyi göster.** Bir container 4. kuralla (isim kalıbı) eşleştiyse, ipucunda "tahmini eşleşme" diye belirt. Kullanıcıya kesin bilgi ile tahmini ayırt ettirmek neden önemli?

---

## Seri burada bitiyor

On bir fazda bir fikirden çalışan bir masaüstü aracına geldik: YAML okuyucu, OOP domain modeli, SQLite kalıcılık, Canvas çizimi, mahalleler, sürükle-bırak, yollar, kullanılabilirlik, zoom ve animasyon, ve nihayet canlı Docker.

Geriye bakınca öne çıkan üç alışkanlık:

- **Önce doküman, sonra kod.** Her fazda "neden böyle" kısmı koddan önce yazıldı; birkaç kez o yazı, kodu yazmadan önce fikrin zayıf olduğunu gösterdi.
- **Hesap Domain'de, çizim App'te.** Test sayısının her fazda artabilmesinin tek sebebi bu; WinUI'yi test etmek zor, saf fonksiyonları test etmek bedava.
- **Yanlışı da yaz.** Eksik migration, çalışmayan tıklama, hatalı zoom öngörüsü, bulanık PNG. Hepsi dokümanlarda duruyor; bir öğretinin en öğretici kısmı zaten orası.

Bundan sonrası opsiyonel: MSIX paketleme, ikon kataloğu için yönetim ekranı, container komutları, Podman desteği, uzak daemon. Plan `docs/PLAN.md`'de; şehir kurulu, gerisi imar.
