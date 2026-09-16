# Faz 1 — YAML'ı Okumak

> **Seri:** DockerCity — docker-compose topoloji dashboard'u
> **Önceki bölüm:** `00-kurulum-ve-iskelet.md` · **Sonraki bölüm:** `02-domain-modeli.md`
> **Tahmini süre:** 2–3 saat
> **Kod reposu:** `C:\Users\burak\Development\coding-vibes\DockerCity`

---

## 1. Bu bölümde ne yapacağız

`samples/docker-compose.yml` dosyasını okuyup, içeriğini **sadakatle** C# nesnelerine çeviren bir okuyucu yazacağız. "Sadakatle" kelimesinin altını çiziyorum: bu fazda hiçbir şeyi yorumlamıyoruz, normalize etmiyoruz, eksik bilgiyi tamamlamıyoruz. Dosyada ne yazıyorsa onu, yazıldığı biçimi de koruyarak alıyoruz.

Sonunda elimizde `DockerCity.Parsing` projesinde çalışan bir `ComposeFileReader` ve onu doğrulayan 15 test olacak.

---

## 2. Neden böyle

### 2.1 Neden ayrı bir DTO katmanı? Doğrudan domain nesnelerine okusak olmaz mı?

Olmaz — daha doğrusu, olur ama pahalıya patlar. İki farklı işi tek sınıfa yüklemiş olursun:

| İş | Kimin derdi |
|---|---|
| YAML'ın şekilsiz gerçekliğini tolere etmek | DTO |
| Anlamlı, tutarlı, doğrulanmış bir model sunmak | Domain |

`environment` alanı bazen map bazen liste geliyor. Bu bir **YAML sorunu**, domain'in umurunda değil — domain sadece "şu servisin şu ortam değişkenleri var" bilgisini ister. Aynı şekilde `container_name`'in yokluğu YAML'da `null`, domain'de ise "Compose bunu türetecek" anlamına gelir.

Bu iki dünyayı ayırdığında, YAML tarafında bir kenar durum çıktığında domain'e dokunmuyorsun. Faz 2'de bunun karşılığını alacağız.

### 2.2 Neden "sadakat" bu kadar önemli?

Bir örnek: sample dosyamızda iki farklı `command` biçimi var.

```yaml
keycloak:
  command: ["start-dev"]                                  # exec form

minio:
  command: server --console-address ":9001" /data         # shell form
```

Bunlar **Docker açısından farklı şeyler**. Exec form komutu doğrudan çalıştırır; shell form onu bir kabuğa verir, yani değişken genişletme, boru, yönlendirme devreye girer. Naif bir parser ikisini de `List<string>` yapıp geçer — ve bu bilgiyi geri dönülmez şekilde kaybeder.

DockerCity bugün bu farkı kullanmıyor. Ama parser'ın işi bilgiyi korumak, ne kadarının kullanılacağına karar vermek değil. **Bilgi kaybı parser'da olursa geri alamazsın; domain'de olursa yeniden hesaplarsın.**

### 2.3 Neden özel converter yazıyoruz?

YamlDotNet bir alanın tek bir şekli olduğunu varsayar. `environment` için `Dictionary<string,string>` dersen liste biçimi patlar; `List<string>` dersen map biçimi patlar. Polimorfik alanlarda düğümü elle okumak gerekir — bunun arayüzü `IYamlTypeConverter`.

---

## 3. Adım adım uygulama

### Adım 1 — Paket

```powershell
cd C:\Users\burak\Development\coding-vibes\DockerCity
dotnet add src\DockerCity.Parsing package YamlDotNet
dotnet add tests\DockerCity.Parsing.Tests reference src\DockerCity.Parsing
```

Yazıldığı tarihte güncel sürüm **18.1.0** ve `net10.0`'ı doğrudan hedefliyor.

> ⚠️ İnternette bulacağın `IYamlTypeConverter` örneklerinin çoğu **eski**. Sürüm 16 ile arayüz değişti: `ReadYaml` ve `WriteYaml` birer parametre daha aldı. Aşağıdaki imzalar güncel olanlar; derleyici "does not implement interface member" derse sebebi budur.

---

### Adım 2 — Test fixture'ı

Test projesine örnek dosyayı koyuyoruz:

```
tests/DockerCity.Parsing.Tests/Fixtures/docker-compose.yml
```

*(Bu dosyayı senin için oluşturdum.)*

`DockerCity.Parsing.Tests.csproj` içine ekle ki çıktı klasörüne kopyalansın:

```xml
<ItemGroup>
  <None Include="Fixtures\docker-compose.yml" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

Fixture'ı test projesine koyuyoruz çünkü o bir **test girdisi**, dokümantasyon örneği değil. Testin ihtiyacı olan her şey testin yanında dursun.

---

### Adım 3 — Polimorfik alanlar için DTO tipleri

`src/DockerCity.Parsing/Dto/ValueBlocks.cs`:

```csharp
namespace DockerCity.Parsing.Dto;

/// <summary>environment alanının yml'de hangi biçimde yazıldığı.</summary>
public enum EnvironmentSyntax
{
    None,
    Mapping,   // POSTGRES_USER: johndoe
    Sequence   // - RABBITMQ_DEFAULT_USER=guest
}

public sealed record EnvEntryDto(string Key, string? Value);

public sealed record EnvironmentBlock(
    EnvironmentSyntax Syntax,
    IReadOnlyList<EnvEntryDto> Entries)
{
    public static readonly EnvironmentBlock Empty =
        new(EnvironmentSyntax.None, []);
}

/// <summary>Docker'da anlamı farklı olan iki command biçimi.</summary>
public enum CommandForm
{
    Exec,    // command: ["start-dev"]
    Shell    // command: server --console-address ":9001" /data
}

public sealed record CommandBlock(
    CommandForm Form,
    string? Raw,
    IReadOnlyList<string> Args);
```

`Syntax` ve `Form` alanlarını saklamamız 2.2'de anlattığım sadakat ilkesinin somut hali. Uygulamanın bugün ihtiyacı yok; yarın "bu compose dosyası hangi stilde yazılmış?" diye sorduğunda cevap elinde olacak.

---

### Adım 4 — Dosya ve servis DTO'ları

`src/DockerCity.Parsing/Dto/ComposeFileDto.cs`:

```csharp
namespace DockerCity.Parsing.Dto;

public sealed class ComposeFileDto
{
    public string? Name { get; set; }
    public Dictionary<string, ComposeServiceDto> Services { get; set; } = [];
    public Dictionary<string, ComposeNetworkDto?> Networks { get; set; } = [];
    public Dictionary<string, ComposeVolumeDto?> Volumes { get; set; } = [];
}

public sealed class ComposeServiceDto
{
    public string? Image { get; set; }
    public string? ContainerName { get; set; }
    public List<string> Ports { get; set; } = [];
    public EnvironmentBlock Environment { get; set; } = EnvironmentBlock.Empty;
    public List<string> Volumes { get; set; } = [];
    public List<string> Networks { get; set; } = [];
    public List<string> DependsOn { get; set; } = [];
    public CommandBlock? Command { get; set; }
    public string? Restart { get; set; }
}

public sealed class ComposeNetworkDto
{
    public string? Driver { get; set; }
    public bool External { get; set; }
}

public sealed class ComposeVolumeDto
{
    public string? Driver { get; set; }
    public bool External { get; set; }
}
```

İki tasarım kararına dikkat:

**`Networks` ve `Volumes` sözlüklerinin değer tipi nullable.** Sample dosyadaki üst seviye tanım şöyle:

```yaml
volumes:
  postgres_data:
  ftp_data:
```

Bu YAML'da "anahtar var, değeri null" demek. `Dictionary<string, ComposeVolumeDto>` dersen `null` ataması patlar. Compose dosyalarının büyük çoğunluğu volume'leri böyle tanımlar, yani bu istisna değil **norm**.

**`ContainerName` nullable, `Networks` ise boş liste.** İkisi de "yok" durumu ama farklı anlamlar taşıyor: `container_name` gerçekten yazılmamış (Compose türetecek), `networks` ise yazılmamış (Compose örtük `default`'a koyacak). Faz 2'de bu ikisi çok farklı davranışlara dönüşecek — şimdiden ayrı tutmakta fayda var.

---

### Adım 5 — Environment converter

`src/DockerCity.Parsing/Converters/EnvironmentConverter.cs`:

```csharp
using DockerCity.Parsing.Dto;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace DockerCity.Parsing.Converters;

public sealed class EnvironmentConverter : IYamlTypeConverter
{
    public bool Accepts(Type type) => type == typeof(EnvironmentBlock);

    public object ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        var entries = new List<EnvEntryDto>();

        // Biçim 1 — map:  POSTGRES_USER: johndoe
        if (parser.TryConsume<MappingStart>(out _))
        {
            while (!parser.TryConsume<MappingEnd>(out _))
            {
                var key = parser.Consume<Scalar>().Value;
                var value = parser.Consume<Scalar>().Value;
                entries.Add(new EnvEntryDto(key, string.IsNullOrEmpty(value) ? null : value));
            }

            return new EnvironmentBlock(EnvironmentSyntax.Mapping, entries);
        }

        // Biçim 2 — liste:  - RABBITMQ_DEFAULT_USER=guest
        if (parser.TryConsume<SequenceStart>(out _))
        {
            while (!parser.TryConsume<SequenceEnd>(out _))
            {
                var raw = parser.Consume<Scalar>().Value;
                var separator = raw.IndexOf('=');

                entries.Add(separator < 0
                    ? new EnvEntryDto(raw, null)                                  // sadece anahtar: host'tan devral
                    : new EnvEntryDto(raw[..separator], raw[(separator + 1)..]));
            }

            return new EnvironmentBlock(EnvironmentSyntax.Sequence, entries);
        }

        // Biçim 3 — boş / null
        parser.TryConsume<Scalar>(out _);
        return EnvironmentBlock.Empty;
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
        => throw new NotSupportedException("DockerCity compose dosyası yazmaz, sadece okur.");
}
```

**Burada olan biten ne?** YamlDotNet'in parser'ı bir **olay akışı** (event stream) sunar: `MappingStart`, `Scalar`, `Scalar`, `MappingEnd`... `Consume<T>()` sıradaki olayı alır ve akışı ilerletir; `TryConsume<T>()` ise "sıradaki olay T mi?" diye bakar, öyleyse tüketir, değilse akışı olduğu gibi bırakır.

Bu yüzden `if (parser.TryConsume<MappingStart>(out _))` kalıbı güvenli: map değilse hiçbir şey tüketilmemiş olur ve bir sonraki `if` denemesine sağlam bir akışla girersin. Elle `Current` kontrol edip `MoveNext` çağırmaya kalkarsan bu davranışı kendin yönetmen gerekir.

`WriteYaml`'da istisna fırlatmak bilinçli. Bu uygulama compose dosyası yazmıyor; sessizce yanlış bir şey üretmektense yüksek sesle "desteklenmiyor" demesi daha iyi. Gün gelir yazma ihtiyacı doğarsa, seni bu satır karşılar.

---

### Adım 6 — Command converter

`src/DockerCity.Parsing/Converters/CommandConverter.cs`:

```csharp
using DockerCity.Parsing.Dto;
using YamlDotNet.Core;
using YamlDotNet.Core.Events;
using YamlDotNet.Serialization;

namespace DockerCity.Parsing.Converters;

public sealed class CommandConverter : IYamlTypeConverter
{
    public bool Accepts(Type type) => type == typeof(CommandBlock);

    public object? ReadYaml(IParser parser, Type type, ObjectDeserializer rootDeserializer)
    {
        // Biçim 1 — exec form:  command: ["start-dev"]
        if (parser.TryConsume<SequenceStart>(out _))
        {
            var args = new List<string>();

            while (!parser.TryConsume<SequenceEnd>(out _))
                args.Add(parser.Consume<Scalar>().Value);

            return new CommandBlock(CommandForm.Exec, null, args);
        }

        // Biçim 2 — shell form:  command: server --console-address ":9001" /data
        var scalar = parser.Consume<Scalar>();

        return string.IsNullOrWhiteSpace(scalar.Value)
            ? null
            : new CommandBlock(CommandForm.Shell, scalar.Value, []);
    }

    public void WriteYaml(IEmitter emitter, object? value, Type type, ObjectSerializer serializer)
        => throw new NotSupportedException("DockerCity compose dosyası yazmaz, sadece okur.");
}
```

Shell form'da komutu **parçalamıyoruz**. `server --console-address ":9001" /data` içinde tırnaklı bir argüman var; doğru parçalamak kabuk alıntılama kurallarını uygulamak demek ve bu parser'ın işi değil. Ham hâliyle saklıyoruz; ihtiyaç olursa ayırma işi sonra, doğru katmanda yapılır.

---

### Adım 7 — Okuyucu

`src/DockerCity.Parsing/ComposeParseException.cs`:

```csharp
namespace DockerCity.Parsing;

public sealed class ComposeParseException : Exception
{
    public ComposeParseException(string message, int line, int column, Exception? inner = null)
        : base(message, inner)
    {
        Line = line;
        Column = column;
    }

    public int Line { get; }
    public int Column { get; }
}
```

`src/DockerCity.Parsing/ComposeFileReader.cs`:

```csharp
using DockerCity.Parsing.Converters;
using DockerCity.Parsing.Dto;
using YamlDotNet.Core;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace DockerCity.Parsing;

public sealed class ComposeFileReader
{
    private readonly IDeserializer _deserializer;

    public ComposeFileReader()
    {
        _deserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .WithTypeConverter(new EnvironmentConverter())
            .WithTypeConverter(new CommandConverter())
            .Build();
    }

    public ComposeFileDto ReadFromText(string yaml)
    {
        ArgumentNullException.ThrowIfNull(yaml);

        try
        {
            return _deserializer.Deserialize<ComposeFileDto>(yaml) ?? new ComposeFileDto();
        }
        catch (YamlException ex)
        {
            throw new ComposeParseException(
                $"Compose dosyası {ex.Start.Line}. satır, {ex.Start.Column}. sütunda okunamadı.",
                ex.Start.Line,
                ex.Start.Column,
                ex);
        }
    }

    public ComposeFileDto ReadFromFile(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        if (!File.Exists(path))
            throw new FileNotFoundException("Compose dosyası bulunamadı.", path);

        return ReadFromText(File.ReadAllText(path));
    }
}
```

Üç satır, üç karar:

**`UnderscoredNamingConvention`** — C#'taki `ContainerName` ile YAML'daki `container_name`, `DependsOn` ile `depends_on` böyle eşleşiyor. Her alana `[YamlMember(Alias = "...")]` yazmaktan kurtarıyor. Dikkat: bu kural yalnızca **özellik adlarına** uygulanır, sözlük anahtarlarına değil — `ftp-server` servis adı olduğu gibi kalır.

**`IgnoreUnmatchedProperties()`** — Compose şeması devasa: `build`, `healthcheck`, `deploy`, `labels`, `profiles`... Biz bunların küçük bir alt kümesini modelliyoruz. Bu satır olmadan, modellemediğimiz ilk anahtarda okuyucu istisna fırlatır. Yani bu satır "gerçek dosyalarla çalışabilmenin" bedeli.

**`ComposeParseException`** — YamlDotNet'in `YamlException`'ını kendi tipimize sarıyoruz ki satır/sütun bilgisi taşınsın. Faz 9'da bozuk bir dosyayı kullanıcıya gösterirken "3. satırda hata" diyebilmek için bu bilgi şimdiden korunmalı.

---

### Adım 8 — Testler

`tests/DockerCity.Parsing.Tests/ComposeFileReaderTests.cs`:

```csharp
using DockerCity.Parsing;
using DockerCity.Parsing.Dto;

namespace DockerCity.Parsing.Tests;

public class ComposeFileReaderTests
{
    private static ComposeFileDto Sample() =>
        new ComposeFileReader().ReadFromFile(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "docker-compose.yml"));

    [Fact]
    public void Butun_servisler_okunur()
    {
        Assert.Equal(10, Sample().Services.Count);
    }

    [Fact]
    public void Tire_iceren_servis_adi_bozulmaz()
    {
        Assert.True(Sample().Services.ContainsKey("ftp-server"));
    }

    // --- Kenar durum 4: environment iki biçimde ---

    [Fact]
    public void Environment_map_biciminde_okunur()
    {
        var env = Sample().Services["postgres"].Environment;

        Assert.Equal(EnvironmentSyntax.Mapping, env.Syntax);
        Assert.Equal("johndoe", env.Entries.Single(e => e.Key == "POSTGRES_USER").Value);
    }

    [Fact]
    public void Environment_liste_biciminde_okunur()
    {
        var env = Sample().Services["rabbitmq"].Environment;

        Assert.Equal(EnvironmentSyntax.Sequence, env.Syntax);
        Assert.Equal("guest", env.Entries.Single(e => e.Key == "RABBITMQ_DEFAULT_USER").Value);
    }

    [Fact]
    public void Sayisal_environment_degeri_metne_cevrilir()
    {
        var env = Sample().Services["ftp-server"].Environment;

        Assert.Equal("21000", env.Entries.Single(e => e.Key == "PASV_MIN_PORT").Value);
    }

    // --- Kenar durum 5: command iki biçimde ---

    [Fact]
    public void Command_exec_biciminde_okunur()
    {
        var command = Sample().Services["keycloak"].Command;

        Assert.NotNull(command);
        Assert.Equal(CommandForm.Exec, command.Form);
        Assert.Equal(["start-dev"], command.Args);
    }

    [Fact]
    public void Command_shell_biciminde_ham_haliyle_korunur()
    {
        var command = Sample().Services["minio"].Command;

        Assert.NotNull(command);
        Assert.Equal(CommandForm.Shell, command.Form);
        Assert.Equal("server --console-address \":9001\" /data", command.Raw);
    }

    // --- Kenar durum 3: port aralığı ---

    [Fact]
    public void Port_araligi_ham_haliyle_korunur()
    {
        var ports = Sample().Services["ftp-server"].Ports;

        Assert.Contains("21:21", ports);
        Assert.Contains("21000-21010:21000-21010", ports);
    }

    // --- Kenar durum 2: container_name yok ---

    [Fact]
    public void Container_name_belirtilmemisse_null_olur()
    {
        Assert.Null(Sample().Services["qdrant"].ContainerName);
        Assert.Equal("fnp-postgres", Sample().Services["postgres"].ContainerName);
    }

    // --- Kenar durum 1: network'süz servisler ---

    [Fact]
    public void Network_belirtilmemis_servisler_bos_liste_dondurur()
    {
        var file = Sample();

        Assert.Empty(file.Services["keycloak"].Networks);
        Assert.Empty(file.Services["minio"].Networks);
        Assert.Equal(["fnp-network"], file.Services["postgres"].Networks);
    }

    // --- Kenar durum 6: üst seviye volume'ler null değerli ---

    [Fact]
    public void Ust_seviye_volumeler_null_degerle_okunur()
    {
        var volumes = Sample().Volumes;

        Assert.Equal(4, volumes.Count);
        Assert.True(volumes.ContainsKey("postgres_data"));
        Assert.Null(volumes["postgres_data"]);
    }

    [Fact]
    public void Network_tanimi_driver_ile_okunur()
    {
        Assert.Equal("bridge", Sample().Networks["fnp-network"]!.Driver);
    }

    // --- Kenar durum 7: opsiyonel restart ---

    [Fact]
    public void Restart_politikasi_sadece_belirtilmisse_dolu_gelir()
    {
        Assert.Equal("always", Sample().Services["qdrant"].Restart);
        Assert.Null(Sample().Services["redis"].Restart);
    }

    [Fact]
    public void Depends_on_okunur()
    {
        Assert.Equal(["postgres"], Sample().Services["pgadmin"].DependsOn);
    }

    [Fact]
    public void Bozuk_yaml_konum_bilgisiyle_hata_verir()
    {
        const string bozuk = "services:\n  web:\n    image: nginx\n   ports:\n";

        var ex = Assert.Throws<ComposeParseException>(
            () => new ComposeFileReader().ReadFromText(bozuk));

        Assert.True(ex.Line > 0);
    }
}
```

---

### Adım 9 — Çalıştır

```powershell
dotnet test tests\DockerCity.Parsing.Tests
```

15 test, hepsi yeşil olmalı. Değilse aşağıya bak.

---

## 4. Takıldığın yerler

**`EnvironmentConverter does not implement interface member 'IYamlTypeConverter.ReadYaml'`**
İki parametreli eski imzayı yazmışsın. Güncel sürümde `ReadYaml(IParser, Type, ObjectDeserializer)` ve `WriteYaml(IEmitter, object?, Type, ObjectSerializer)`. İnternetteki örneklerin çoğu sürüm 16 öncesine ait.

**`Property 'build' not found on type ComposeServiceDto`**
`IgnoreUnmatchedProperties()` çağrısını unutmuşsun.

**Test `FileNotFoundException` veriyor.**
Fixture çıktı klasörüne kopyalanmamış. `csproj`'daki `CopyToOutputDirectory="PreserveNewest"` satırını kontrol et, sonra `dotnet build` ile yeniden derle.

**`environment` map biçimi okunuyor ama liste biçiminde `InvalidOperationException` alıyorum.**
Converter'da `SequenceStart` dalına girmeden önce `MappingStart` denemesinde akışı tükettiysen olur. `TryConsume` kullandığından emin ol — `Consume` başarısız olduğunda akışı bozar.

**Servis sayısı 10 değil 9 çıkıyor.**
Fixture dosyasında girinti kaymış olabilir. `keycloak` bloğunun `services:` altında ve diğerleriyle aynı hizada olduğunu doğrula — sample dosyanın orijinalinde o satırdan önce iki boşluk var.

**`PASV_MIN_PORT` testi `"21000"` yerine başka bir şey döndürüyor.**
YAML'da tırnaksız `21000` bir tam sayı. Biz converter içinde `Consume<Scalar>().Value` ile **ham metni** okuduğumuz için `"21000"` gelir. Eğer `Dictionary<string,string>` ile okusaydın YamlDotNet dönüşümü kendisi yapacaktı — sonuç aynı, ama yolu farklı.

---

## 5. Ne öğrendik

- **DTO ile domain ayrımı, değişim maliyetini sınırlar.** YAML'ın tuhaflıkları DTO'da kalır; domain temiz doğar.
- **Parser'ın işi bilgiyi korumaktır, yorumlamak değil.** `CommandForm.Exec` ile `Shell` ayrımını bugün kullanmıyoruz ama kaybetmedik. Parser'da kaybedilen bilgi geri gelmez.
- **YamlDotNet bir olay akışı sunar.** `Consume` tüketip ilerletir, `TryConsume` bakar ve sadece eşleşirse tüketir. Polimorfik alanları güvenle denemenin yolu bu.
- **Gerçek dünya şeması her zaman modellediğinden büyüktür.** `IgnoreUnmatchedProperties()` bir gevşeklik değil, sürüm uyumluluğu stratejisi.
- **"Yok" tek bir şey değildir.** `container_name` yokluğu ile `networks` yokluğu farklı sonuçlar doğurur; ikisini aynı şekilde temsil edersen Faz 2'de ayıramazsın.

---

## 6. Kendin dene

1. **Tırnaksız port tuzağını keşfet.** Fixture'ın bir kopyasında `- "5432:5432"` satırını tırnaksız `- 5432:5432` yap ve testi çalıştır. Ne oluyor? (İpucu: YAML'da `a:b` bir eşleme olabilir.) Compose dosyalarının portları neden hep tırnak içinde yazdığını bu deneyden sonra kendi cümlelerinle açıkla. Bu, Docker dünyasının en bilinen ayak kurşunlarından biri.

2. **`depends_on`'un uzun biçimini destekle.** Compose şu yazımı da kabul eder:
   ```yaml
   depends_on:
     postgres:
       condition: service_healthy
   ```
   `List<string>` bunu okuyamaz. Kendi `DependsOnConverter`'ını yaz; hem liste hem map biçimini kabul etsin ve `condition` bilgisini korusun.

3. **`ports`'un uzun biçimini araştır.** Compose `- target: 5432` / `published: 5432` biçimini de destekler. Fixture'a böyle bir servis ekle ve mevcut parser'ın ne yaptığına bak. Kırılıyor mu, sessizce yanlış mı okuyor? İkincisi birincisinden kötüdür — neden?

4. **Boş bir dosya ver.** `ReadFromText("")` ne döndürüyor? `null` mı, boş `ComposeFileDto` mu? Davranışı bir testle sabitle. *(İpucu: `?? new ComposeFileDto()` satırı tam da bunun için orada.)*

---

**Sonraki bölüm:** `02-domain-modeli.md` — bu DTO'ları alıp abstract base'li bir OOP hiyerarşisine, `ServiceFactory`'ye ve `CityMap`'e dönüştüreceğiz. Orada asıl mesele şu olacak: `keycloak` ile `minio` hiçbir network'e bağlı değil, ama Compose semantiğinde ikisi de örtük bir `default` ağındalar. O ağı **parser değil domain üretecek** — çünkü o, dosyada yazan bir şey değil, dosyanın anlamından çıkan bir şey.
