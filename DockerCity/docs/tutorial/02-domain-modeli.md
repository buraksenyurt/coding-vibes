# Faz 2 — Domain Modeli ve OOP Hiyerarşisi

> **Seri:** DockerCity — docker-compose topoloji dashboard'u
> **Önceki bölüm:** `01-yaml-parser.md` · **Sonraki bölüm:** `03-sqlite-ef-core.md`
> **Tahmini süre:** 3–4 saat
> **Kod reposu:** `C:\Users\burak\Development\coding-vibes\DockerCity`

---

## 1. Bu bölümde ne yapacağız

Faz 1'de dosyayı sadakatle okuduk ama hiçbir şeyi **anlamadık**. `"21000-21010:21000-21010"` hâlâ bir metin. `keycloak`'ın hangi ağda olduğunu bilmiyoruz. `postgres`'in bir veritabanı olduğundan haberimiz yok.

Bu fazda o metinleri anlama çeviriyoruz: değer nesneleri, kalıtım hiyerarşisi, bir fabrika ve şehrin kendisi — `CityMap`.

Sonunda `samples/docker-compose.yml`'dan 10 servis, 2 mahalle (biri **örtük**) ve doğrulanmış ilişkiler içeren bir nesne grafiği çıkacak.

---

## 2. Neden böyle

### 2.1 Plandan sapıyoruz: `ServiceFactory` nerede yaşamalı?

Planda `ServiceFactory`'yi Domain'e koymuştum. Yazmaya kalkınca şu çıkıyor:

```csharp
// DockerCity.Domain içinde
public ComposeService Create(string key, ComposeServiceDto dto)  // ← ComposeServiceDto nereden gelecek?
```

`ComposeServiceDto` `DockerCity.Parsing`'de. Domain'den ona ulaşmak için Domain'in Parsing'e referans vermesi gerekir — ve bu, Faz 0'da kurduğumuz ok yönünü tersine çevirir.

**Derleyici burada bize bir tasarım hatasını gösteriyor.** Fabrika bir *çeviri* işi yapıyor: bir dosya biçiminden domain'e. Bu Parsing'in sorumluluğu. Domain, compose diye bir dosya formatının varlığından haberdar olmamalı.

Yani:

| Katman | Ne var |
|---|---|
| `DockerCity.Domain` | `ComposeService` hiyerarşisi, değer nesneleri, `CityMap`, `IServiceCategoryResolver` |
| `DockerCity.Parsing` | DTO'lar, okuyucu, **`ServiceFactory`**, **`CityMapBuilder`** |

Faz 0'daki "bağımlılık yönü bir mimari araçtır" cümlesinin ilk somut faydası bu. Kuralı yorum satırına yazsaydık kimse fark etmezdi.

### 2.2 Kalıtım burada gerçekten gerekli mi?

Dürüst cevap: **plandaki hâliyle gerekli değildi.**

`DatabaseService`, `MessagingService`, `StorageService`... hepsi sadece bir `Category` özelliği döndürseydi, bu altı sınıf bir `enum` alanının şişirilmiş hâli olurdu. Böyle bir hiyerarşi kalıtımı öğretmez, kalıtımın kötüye kullanımını öğretir.

Kalıtımın hakkını vermesi için alt tiplerin **davranışının** farklılaşması gerekir. Sample dosyaya bakınca gerçek farklar var:

- `rabbitmq` iki port yayınlıyor: `5672` broker, `15672` yönetim arayüzü. Hangisinin ne olduğunu bilmek servis türüne bağlı.
- `minio` `9000` API, `9001` konsol.
- `postgres` bir named volume'a veri yazıyor — yani kalıcı. `redis` yazmıyor — yani kap silinince veri gider.
- `pgadmin`, `sonarqube`, `keycloak` tarayıcıdan açılabilir; `nats` açılamaz.

Bunlar dashboard'da doğrudan işimize yarayacak gerçek davranışlar. Ama dikkat: "web arayüzü var" özelliği tek bir dalda değil, hiyerarşinin farklı yerlerinde. Bunu kalıtımla çözmeye kalkarsan `WebAccessibleDatabaseService` gibi saçmalıklara varırsın.

**Kural:** kalıtım "bu *ne*?" sorusunu, arayüz "bu *ne yapabilir*?" sorusunu cevaplar.

```
ComposeService (abstract)          ← ne olduğu
    ├── DatabaseService        : IDataPersisting
    ├── MessagingService       : IWebAccessible
    ├── StorageService         : IWebAccessible, IDataPersisting
    ├── IdentityService        : IWebAccessible
    ├── ToolingService         : IWebAccessible
    └── GenericService
```

### 2.3 Aynı gerçeği iki kez modelleme

Planda üç ilişki tipi vardı: `DependsOn`, `SharedNetwork`, `SharedVolume`. `SharedNetwork`'ü **çıkarıyoruz**.

Sebep: `fnp-network`'te 8 servis var. "Aynı ağdalar" ilişkisini kenar olarak üretirsen 8×7/2 = **28 kenar** çıkar ve hiçbirini çizmeyeceksin — çünkü bu ilişkiyi zaten mahalle sınırıyla gösteriyoruz.

Bir gerçeği iki yerde tutarsan ikisini senkron tutmak zorunda kalırsın. Mahalle üyeliği o bilgiyi zaten taşıyor.

---

## 3. Adım adım uygulama

Bütün bu bölüm `DockerCity.Domain` projesinde başlıyor.

### Adım 1 — Değer nesneleri

`src/DockerCity.Domain/Values/ImageRef.cs`:

```csharp
using System.Globalization;

namespace DockerCity.Domain.Values;

/// <summary>Bir Docker image referansının ayrıştırılmış hâli.</summary>
public readonly record struct ImageRef(
    string? Registry,
    string Repository,
    string Tag,
    string? Digest)
{
    public bool IsLatest => Digest is null && Tag == "latest";

    public string DisplayName =>
        Repository.Contains('/') ? Repository[(Repository.LastIndexOf('/') + 1)..] : Repository;

    public static ImageRef Parse(string raw)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(raw);

        var value = raw.Trim();
        string? digest = null;

        // postgres@sha256:abc...
        var at = value.IndexOf('@');
        if (at >= 0)
        {
            digest = value[(at + 1)..];
            value = value[..at];
        }

        // Registry mi, namespace mi?
        string? registry = null;
        var slash = value.IndexOf('/');
        if (slash > 0)
        {
            var head = value[..slash];
            if (head.Contains('.') || head.Contains(':') || head == "localhost")
            {
                registry = head;
                value = value[(slash + 1)..];
            }
        }

        var tag = "latest";
        var colon = value.LastIndexOf(':');
        if (colon >= 0)
        {
            tag = value[(colon + 1)..];
            value = value[..colon];
        }

        return new ImageRef(registry, value, tag, digest);
    }
}
```

> **Buradaki tek incelikli kural:** `quay.io/keycloak/keycloak` ile `qdrant/qdrant` aynı şekle sahip ama farklı şeyler. Docker'ın kuralı şu: ilk parça **nokta, iki nokta içeriyorsa ya da `localhost` ise** registry'dir; aksi halde namespace'tir. Yani `quay.io` bir registry, `qdrant` ise Docker Hub'daki bir kullanıcı adı.
>
> Bunu bilmeden yazılmış her image parser'ı `qdrant/qdrant`'ı yanlış okur.

`src/DockerCity.Domain/Values/PortMapping.cs`:

```csharp
using System.Globalization;

namespace DockerCity.Domain.Values;

public sealed record PortMapping(
    string? HostIp,
    int? HostStart,
    int? HostEnd,
    int ContainerStart,
    int ContainerEnd,
    string Protocol)
{
    public bool IsRange => ContainerEnd > ContainerStart;

    /// <summary>Host'a yayınlanmamış port sadece ağ içinden erişilebilir.</summary>
    public bool IsPublished => HostStart.HasValue;

    public static PortMapping Parse(string raw)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(raw);

        var value = raw.Trim();
        var protocol = "tcp";

        var slash = value.LastIndexOf('/');
        if (slash >= 0)
        {
            protocol = value[(slash + 1)..];
            value = value[..slash];
        }

        var parts = value.Split(':');

        return parts.Length switch
        {
            // "6379" — sadece kap portu, host'a yayınlanmıyor
            1 => Build(null, null, parts[0], protocol),

            // "5050:80"
            2 => Build(null, parts[0], parts[1], protocol),

            // "127.0.0.1:5432:5432"
            3 => Build(parts[0], parts[1], parts[2], protocol),

            _ => throw new FormatException($"Port eşlemesi anlaşılamadı: '{raw}'")
        };
    }

    private static PortMapping Build(string? hostIp, string? host, string container, string protocol)
    {
        var (containerStart, containerEnd) = ParseRange(container);

        if (host is null)
            return new PortMapping(hostIp, null, null, containerStart, containerEnd, protocol);

        var (hostStart, hostEnd) = ParseRange(host);
        return new PortMapping(hostIp, hostStart, hostEnd, containerStart, containerEnd, protocol);
    }

    private static (int Start, int End) ParseRange(string text)
    {
        var dash = text.IndexOf('-');

        if (dash < 0)
        {
            var single = int.Parse(text, CultureInfo.InvariantCulture);
            return (single, single);
        }

        return (
            int.Parse(text[..dash], CultureInfo.InvariantCulture),
            int.Parse(text[(dash + 1)..], CultureInfo.InvariantCulture));
    }
}
```

`src/DockerCity.Domain/Values/VolumeMount.cs`:

```csharp
namespace DockerCity.Domain.Values;

public enum VolumeMountKind
{
    Named,       // postgres_data:/var/lib/postgres/data
    Bind,        // ./config:/etc/app
    Anonymous    // /var/lib/data
}

public sealed record VolumeMount(
    VolumeMountKind Kind,
    string? Source,
    string Target,
    bool IsReadOnly)
{
    public static VolumeMount Parse(string raw)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(raw);

        var parts = raw.Trim().Split(':');
        var isReadOnly = parts.Length > 2 && parts[^1] == "ro";

        if (parts.Length == 1)
            return new VolumeMount(VolumeMountKind.Anonymous, null, parts[0], isReadOnly);

        var source = parts[0];
        var target = parts[1];

        var kind = source.StartsWith('/') || source.StartsWith('.') || source.StartsWith('~')
            ? VolumeMountKind.Bind
            : VolumeMountKind.Named;

        return new VolumeMount(kind, source, target, isReadOnly);
    }
}
```

`src/DockerCity.Domain/Values/EnvVariable.cs`:

```csharp
namespace DockerCity.Domain.Values;

public sealed record EnvVariable(string Key, string? Value)
{
    private static readonly string[] SecretMarkers =
        ["PASSWORD", "PASS", "SECRET", "TOKEN", "APIKEY", "API_KEY", "PRIVATE"];

    /// <summary>Değeri arayüzde maskelenmeli mi?</summary>
    public bool IsSensitive =>
        SecretMarkers.Any(marker => Key.Contains(marker, StringComparison.OrdinalIgnoreCase));

    public string DisplayValue => IsSensitive ? "••••••" : Value ?? "";
}
```

Bunu ekliyoruz çünkü sample dosyada beş tane parola var ve balon pencerede `POSTGRES_PASSWORD: somew0rds` göstermek istemeyiz. Dashboard'un birine ekran paylaşımıyla gösterileceğini düşün.

---

### Adım 2 — Ortak taban

`src/DockerCity.Domain/ComposeElement.cs`:

```csharp
namespace DockerCity.Domain;

/// <summary>Compose dosyasında adı olan her şeyin ortak tabanı.</summary>
public abstract class ComposeElement
{
    protected ComposeElement(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        Name = name;
    }

    /// <summary>yml'deki anahtar. Şehirde bu ad benzersizdir.</summary>
    public string Name { get; }

    /// <summary>Arayüzde gösterilecek tek satırlık özet.</summary>
    public abstract string Describe();
}
```

`Describe()`'ı `ToString()` yerine ayrı bir üye olarak tanımlıyoruz. `ToString()` hata ayıklayıcının ve log'ların kullandığı şeydir; kullanıcı arayüzü metnini oraya koyarsan ikisi zamanla birbirine karışır ve "neden log'da emoji var?" diye sorarsın.

---

### Adım 3 — Yetenek arayüzleri

`src/DockerCity.Domain/Capabilities.cs`:

```csharp
using DockerCity.Domain.Values;

namespace DockerCity.Domain;

/// <summary>Tarayıcıdan açılabilen bir arayüzü olan servis.</summary>
public interface IWebAccessible
{
    /// <summary>Web arayüzünün yayınlandığı port. Yayınlanmamışsa null.</summary>
    PortMapping? WebPort { get; }

    Uri? WebUrl { get; }
}

/// <summary>Named volume üzerinde kalıcı veri tutan servis.</summary>
public interface IDataPersisting
{
    IReadOnlyList<VolumeMount> DataVolumes { get; }

    bool HasPersistentData { get; }
}
```

---

### Adım 4 — Servis tabanı

`src/DockerCity.Domain/ComposeService.cs`:

```csharp
using DockerCity.Domain.Values;

namespace DockerCity.Domain;

public enum ServiceCategory
{
    Unknown,
    Database,
    Messaging,
    Storage,
    Identity,
    Tooling
}

public enum RestartPolicy
{
    No,
    Always,
    OnFailure,
    UnlessStopped
}

public abstract class ComposeService : ComposeElement
{
    protected ComposeService(string name, ServiceDefinition definition) : base(name)
    {
        ArgumentNullException.ThrowIfNull(definition);

        Image = definition.Image;
        ContainerName = definition.ContainerName;
        Ports = definition.Ports;
        Environment = definition.Environment;
        Volumes = definition.Volumes;
        NetworkNames = definition.NetworkNames;
        DependsOn = definition.DependsOn;
        Command = definition.Command;
        Restart = definition.Restart;
    }

    public ImageRef Image { get; }

    /// <summary>yml'de yazmıyorsa null — Compose bunu kendisi türetir.</summary>
    public string? ContainerName { get; }

    public IReadOnlyList<PortMapping> Ports { get; }
    public IReadOnlyList<EnvVariable> Environment { get; }
    public IReadOnlyList<VolumeMount> Volumes { get; }
    public IReadOnlyList<string> NetworkNames { get; }
    public IReadOnlyList<string> DependsOn { get; }
    public CommandSpec? Command { get; }
    public RestartPolicy Restart { get; }

    /// <summary>Alt sınıfın kim olduğunu bildirmesi zorunlu.</summary>
    public abstract ServiceCategory Category { get; }

    /// <summary>Alt sınıf isterse kendi ikonunu söyler.</summary>
    public virtual string IconKey => Image.DisplayName;

    public bool IsSupervised => Restart is RestartPolicy.Always or RestartPolicy.UnlessStopped;

    public IReadOnlyList<PortMapping> PublishedPorts =>
        [.. Ports.Where(port => port.IsPublished)];

    public override string Describe() =>
        $"{Name} — {Image.Repository}:{Image.Tag}";
}
```

`ServiceDefinition`, kurucuya dokuz parametre yazmamak için var. `src/DockerCity.Domain/ServiceDefinition.cs`:

```csharp
using DockerCity.Domain.Values;

namespace DockerCity.Domain;

/// <summary>Bir servisi kurmak için gereken ham malzeme.</summary>
public sealed record ServiceDefinition
{
    public required ImageRef Image { get; init; }
    public string? ContainerName { get; init; }
    public IReadOnlyList<PortMapping> Ports { get; init; } = [];
    public IReadOnlyList<EnvVariable> Environment { get; init; } = [];
    public IReadOnlyList<VolumeMount> Volumes { get; init; } = [];
    public IReadOnlyList<string> NetworkNames { get; init; } = [];
    public IReadOnlyList<string> DependsOn { get; init; } = [];
    public CommandSpec? Command { get; init; }
    public RestartPolicy Restart { get; init; } = RestartPolicy.No;
}

public enum CommandKind { Shell, Exec }

public sealed record CommandSpec(CommandKind Kind, string? Raw, IReadOnlyList<string> Arguments);
```

---

### Adım 5 — Alt sınıflar

Altı sınıfın ikisini tam gösteriyorum; kalanlar aynı kalıpta.

`src/DockerCity.Domain/Services/DatabaseService.cs`:

```csharp
using DockerCity.Domain.Values;

namespace DockerCity.Domain.Services;

public sealed class DatabaseService : ComposeService, IDataPersisting
{
    public DatabaseService(string name, ServiceDefinition definition)
        : base(name, definition) { }

    public override ServiceCategory Category => ServiceCategory.Database;

    public IReadOnlyList<VolumeMount> DataVolumes =>
        [.. Volumes.Where(volume => volume.Kind == VolumeMountKind.Named)];

    public bool HasPersistentData => DataVolumes.Count > 0;

    public override string Describe() => HasPersistentData
        ? $"{Name} — veritabanı, verisi kalıcı"
        : $"{Name} — veritabanı, verisi geçici";
}
```

`src/DockerCity.Domain/Services/MessagingService.cs`:

```csharp
using DockerCity.Domain.Values;

namespace DockerCity.Domain.Services;

public sealed class MessagingService : ComposeService, IWebAccessible
{
    /// <summary>Bilinen yönetim arayüzü portları (kap tarafı).</summary>
    private static readonly int[] KnownManagementPorts = [15672, 8222, 8161];

    public MessagingService(string name, ServiceDefinition definition)
        : base(name, definition) { }

    public override ServiceCategory Category => ServiceCategory.Messaging;

    /// <summary>Mesajların aktığı port — yönetim arayüzü olmayan ilk yayınlanmış port.</summary>
    public PortMapping? BrokerPort =>
        PublishedPorts.FirstOrDefault(port => !KnownManagementPorts.Contains(port.ContainerStart));

    public PortMapping? WebPort =>
        PublishedPorts.FirstOrDefault(port => KnownManagementPorts.Contains(port.ContainerStart));

    public Uri? WebUrl => WebPort is { HostStart: int host }
        ? new Uri($"http://localhost:{host}")
        : null;

    public override string Describe() => WebPort is null
        ? $"{Name} — mesajlaşma"
        : $"{Name} — mesajlaşma, yönetim arayüzü {WebUrl}";
}
```

`BrokerPort` / `WebPort` ayrımı, 2.2'de bahsettiğim "kalıtımın hakkını vermesi" meselesinin somut hâli. `rabbitmq` için `5672` ile `15672`'yi ayırt etmek **mesajlaşma servisi olmakla ilgili bir bilgi**. Bunu `ComposeService` tabanına koyarsan `postgres` de "broker portum hangisi?" diye sorabilir hâle gelir — anlamsız.

Kalan dördü:

| Sınıf | Arayüzler | Ayırt edici davranış |
|---|---|---|
| `StorageService` | `IWebAccessible`, `IDataPersisting` | Konsol portu ile API portunu ayırır (minio: 9001 konsol, 9000 API) |
| `IdentityService` | `IWebAccessible` | Tek web portu; `WebUrl` doğrudan ondan |
| `ToolingService` | `IWebAccessible` | Tek web portu; `IconKey` için image adını kullanır |
| `GenericService` | — | `Category => Unknown`, `Describe()` tabandaki hâliyle kalır |

`StorageService`'de bilinen konsol portları `[9001, 9090]`, `IdentityService` ve `ToolingService`'de ise `WebPort => PublishedPorts.FirstOrDefault()` yeterli.

---

### Adım 6 — Kategori çözücü

`src/DockerCity.Domain/IServiceCategoryResolver.cs`:

```csharp
using DockerCity.Domain.Values;

namespace DockerCity.Domain;

public interface IServiceCategoryResolver
{
    ServiceCategory Resolve(ImageRef image);
}
```

`src/DockerCity.Domain/InMemoryServiceCategoryResolver.cs`:

```csharp
using DockerCity.Domain.Values;

namespace DockerCity.Domain;

public sealed class InMemoryServiceCategoryResolver : IServiceCategoryResolver
{
    private static readonly (string Pattern, ServiceCategory Category)[] Table =
    [
        ("postgres",                     ServiceCategory.Database),
        ("mysql",                        ServiceCategory.Database),
        ("mariadb",                      ServiceCategory.Database),
        ("mongo",                        ServiceCategory.Database),
        ("redis",                        ServiceCategory.Database),
        ("qdrant/qdrant",                ServiceCategory.Database),
        ("rabbitmq",                     ServiceCategory.Messaging),
        ("nats",                         ServiceCategory.Messaging),
        ("minio/minio",                  ServiceCategory.Storage),
        ("delfer/alpine-ftp-server",     ServiceCategory.Storage),
        ("keycloak/keycloak",            ServiceCategory.Identity),
        ("dpage/pgadmin4",               ServiceCategory.Tooling),
        ("sonarqube",                    ServiceCategory.Tooling),
    ];

    public ServiceCategory Resolve(ImageRef image)
    {
        ArgumentNullException.ThrowIfNull(image);

        // Uzun desen önce kazansın: "redis" ile "redis/redis-stack" çakışmasın.
        foreach (var (pattern, category) in Table.OrderByDescending(row => row.Pattern.Length))
        {
            if (image.Repository.Equals(pattern, StringComparison.OrdinalIgnoreCase) ||
                image.Repository.EndsWith('/' + pattern, StringComparison.OrdinalIgnoreCase))
            {
                return category;
            }
        }

        return ServiceCategory.Unknown;
    }
}
```

Bu sınıf Faz 3'te SQLite destekli bir uygulamayla **değişecek**. Arayüzü şimdiden ayırmamızın sebebi bu: Faz 3'te `ServiceFactory`'ye dokunmayacağız, sadece kurucuya başka bir `IServiceCategoryResolver` vereceğiz.

---

### Adım 7 — Şehir haritası

`src/DockerCity.Domain/CityMap.cs`:

```csharp
namespace DockerCity.Domain;

public enum CityLinkKind
{
    /// <summary>depends_on — yönlü, şehirde yol olarak çizilir.</summary>
    DependsOn,

    /// <summary>Aynı named volume'u kullanan iki servis — yönsüz.</summary>
    SharedVolume
}

public sealed record CityLink(CityLinkKind Kind, string From, string To);

/// <summary>Bir network'e karşılık gelen mahalle.</summary>
public sealed class District : ComposeElement
{
    public District(string name, bool isImplicit, IReadOnlyList<ComposeService> members)
        : base(name)
    {
        IsImplicit = isImplicit;
        Members = members;
    }

    /// <summary>yml'de yazmıyor; Compose semantiğinden türetildi.</summary>
    public bool IsImplicit { get; }

    public IReadOnlyList<ComposeService> Members { get; }

    public override string Describe() => IsImplicit
        ? $"{Name} (örtük) — {Members.Count} servis"
        : $"{Name} — {Members.Count} servis";
}

public sealed class CityMap
{
    public CityMap(
        IReadOnlyList<ComposeService> services,
        IReadOnlyList<District> districts,
        IReadOnlyList<CityLink> links)
    {
        Services = services;
        Districts = districts;
        Links = links;
    }

    public IReadOnlyList<ComposeService> Services { get; }
    public IReadOnlyList<District> Districts { get; }
    public IReadOnlyList<CityLink> Links { get; }

    public ComposeService? Find(string name) =>
        Services.FirstOrDefault(service => service.Name == name);
}
```

---

### Adım 8 — Fabrika (artık `DockerCity.Parsing`'de)

`src/DockerCity.Parsing/Mapping/ServiceFactory.cs`:

```csharp
using DockerCity.Domain;
using DockerCity.Domain.Services;
using DockerCity.Domain.Values;
using DockerCity.Parsing.Dto;

namespace DockerCity.Parsing.Mapping;

public sealed class ServiceFactory
{
    private readonly IServiceCategoryResolver _resolver;

    public ServiceFactory(IServiceCategoryResolver resolver)
    {
        ArgumentNullException.ThrowIfNull(resolver);
        _resolver = resolver;
    }

    public ComposeService Create(string name, ComposeServiceDto dto)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(dto.Image))
            throw new InvalidOperationException($"'{name}' servisinde image alanı yok.");

        var image = ImageRef.Parse(dto.Image);

        var definition = new ServiceDefinition
        {
            Image = image,
            ContainerName = dto.ContainerName,
            Ports = [.. dto.Ports.Select(PortMapping.Parse)],
            Environment = [.. dto.Environment.Entries.Select(e => new EnvVariable(e.Key, e.Value))],
            Volumes = [.. dto.Volumes.Select(VolumeMount.Parse)],
            NetworkNames = [.. dto.Networks],
            DependsOn = [.. dto.DependsOn],
            Command = MapCommand(dto.Command),
            Restart = MapRestart(dto.Restart)
        };

        // Factory Method: kategoriye göre doğru alt tip.
        return _resolver.Resolve(image) switch
        {
            ServiceCategory.Database  => new DatabaseService(name, definition),
            ServiceCategory.Messaging => new MessagingService(name, definition),
            ServiceCategory.Storage   => new StorageService(name, definition),
            ServiceCategory.Identity  => new IdentityService(name, definition),
            ServiceCategory.Tooling   => new ToolingService(name, definition),
            _                         => new GenericService(name, definition)
        };
    }

    private static CommandSpec? MapCommand(CommandBlock? block) => block is null
        ? null
        : new CommandSpec(
            block.Form == CommandForm.Exec ? CommandKind.Exec : CommandKind.Shell,
            block.Raw,
            block.Arguments);

    private static RestartPolicy MapRestart(string? raw) => raw switch
    {
        "always"         => RestartPolicy.Always,
        "on-failure"     => RestartPolicy.OnFailure,
        "unless-stopped" => RestartPolicy.UnlessStopped,
        _                => RestartPolicy.No
    };
}
```

`MapCommand` ve `MapRestart`'ın burada olması dikkat çekici: `CommandForm` Parsing'in tipi, `CommandKind` Domain'in tipi. İki paralel enum tutmak ilk bakışta gereksiz görünür ama tam da aradığımız ayrım bu — Parsing "yml'de hangi sözdizimi kullanılmış" diyor, Domain "komut nasıl çalışacak" diyor. Yarın compose dışında bir kaynak eklersek Domain'in enum'u aynı kalır.

---

### Adım 9 — Şehir kurucusu ve **örtük ağ**

Bu fazın can alıcı yeri.

`src/DockerCity.Parsing/Mapping/CityMapBuilder.cs`:

```csharp
using DockerCity.Domain;
using DockerCity.Domain.Values;
using DockerCity.Parsing.Dto;

namespace DockerCity.Parsing.Mapping;

public sealed class CityMapBuilder
{
    public const string DefaultNetworkName = "default";

    private readonly ServiceFactory _factory;

    public CityMapBuilder(ServiceFactory factory)
    {
        ArgumentNullException.ThrowIfNull(factory);
        _factory = factory;
    }

    public CityMap Build(ComposeFileDto file)
    {
        ArgumentNullException.ThrowIfNull(file);

        var services = file.Services
            .Select(entry => _factory.Create(entry.Key, entry.Value))
            .ToList();

        return new CityMap(services, BuildDistricts(file, services), BuildLinks(services));
    }

    private static List<District> BuildDistricts(
        ComposeFileDto file,
        IReadOnlyList<ComposeService> services)
    {
        var districts = new List<District>();

        // 1) yml'de açıkça tanımlanmış ağlar
        foreach (var networkName in file.Networks.Keys)
        {
            var members = services
                .Where(service => service.NetworkNames.Contains(networkName))
                .ToList();

            districts.Add(new District(networkName, isImplicit: false, members));
        }

        // 2) Hiçbir ağ belirtmemiş servisler örtük 'default' ağına girer.
        var orphans = services.Where(service => service.NetworkNames.Count == 0).ToList();

        if (orphans.Count > 0)
        {
            // Dosya kendi 'default' ağını tanımlamışsa o ağ örtük değildir.
            var existing = districts.FirstOrDefault(d => d.Name == DefaultNetworkName);

            if (existing is null)
            {
                districts.Add(new District(DefaultNetworkName, isImplicit: true, orphans));
            }
            else
            {
                districts.Remove(existing);
                districts.Add(new District(
                    DefaultNetworkName,
                    isImplicit: false,
                    [.. existing.Members, .. orphans]));
            }
        }

        return districts;
    }

    private static List<CityLink> BuildLinks(IReadOnlyList<ComposeService> services)
    {
        var links = new List<CityLink>();
        var names = services.Select(service => service.Name).ToHashSet(StringComparer.Ordinal);

        // depends_on — yönlü
        foreach (var service in services)
        {
            foreach (var target in service.DependsOn)
            {
                if (!names.Contains(target))
                {
                    throw new InvalidOperationException(
                        $"'{service.Name}' servisi tanımsız '{target}' servisine bağımlı.");
                }

                links.Add(new CityLink(CityLinkKind.DependsOn, service.Name, target));
            }
        }

        // Paylaşılan named volume — yönsüz, her çift bir kez
        var byVolume = services
            .SelectMany(service => service.Volumes
                .Where(volume => volume.Kind == VolumeMountKind.Named && volume.Source is not null)
                .Select(volume => (Volume: volume.Source!, Service: service.Name)))
            .GroupBy(pair => pair.Volume, StringComparer.Ordinal);

        foreach (var group in byVolume)
        {
            var users = group.Select(pair => pair.Service).Distinct(StringComparer.Ordinal)
                             .OrderBy(name => name, StringComparer.Ordinal).ToList();

            for (var i = 0; i < users.Count; i++)
            {
                for (var j = i + 1; j < users.Count; j++)
                    links.Add(new CityLink(CityLinkKind.SharedVolume, users[i], users[j]));
            }
        }

        return links;
    }
}
```

**Örtük ağ neden burada üretiliyor?**

Faz 1'de "parser yorumlamaz, korur" demiştik ve `keycloak`'ın `networks` listesini boş bıraktık — çünkü dosyada gerçekten yazmıyor. Ama Compose *semantiğinde* o servis `default` ağında.

Bu bilgi dosyada değil, **dosyanın anlamında**. Anlam üretmek domain tarafının işi. Parser bir satır uydursaydı, sonra "bu gerçekten yazıyor muydu?" sorusunun cevabı kaybolurdu. Şimdi ikisi de elimizde: `service.NetworkNames` boş (dosyada yoktu) ama `District.IsImplicit` true (anlamı bu).

Arayüzde de bunu göstereceğiz — örtük mahallenin sınırı kesikli çizilecek.

---

### Adım 10 — Testler

`tests/DockerCity.Domain.Tests/ImageRefTests.cs`:

```csharp
using DockerCity.Domain.Values;

namespace DockerCity.Domain.Tests;

public class ImageRefTests
{
    [Theory]
    [InlineData("postgres:latest",                     null,      "postgres",          "latest")]
    [InlineData("postgres",                            null,      "postgres",          "latest")]
    [InlineData("rabbitmq:3-management",               null,      "rabbitmq",          "3-management")]
    [InlineData("dpage/pgadmin4:latest",               null,      "dpage/pgadmin4",    "latest")]
    [InlineData("qdrant/qdrant",                       null,      "qdrant/qdrant",     "latest")]
    [InlineData("quay.io/keycloak/keycloak:latest", "quay.io", "keycloak/keycloak", "latest")]
    public void Image_referansi_dogru_ayristirilir(
        string raw, string? registry, string repository, string tag)
    {
        var image = ImageRef.Parse(raw);

        Assert.Equal(registry, image.Registry);
        Assert.Equal(repository, image.Repository);
        Assert.Equal(tag, image.Tag);
    }

    [Fact]
    public void Noktasiz_ilk_parca_registry_sayilmaz()
    {
        // 'qdrant' bir Docker Hub kullanıcı adı, registry değil.
        Assert.Null(ImageRef.Parse("qdrant/qdrant").Registry);

        // 'quay.io' nokta içerdiği için registry.
        Assert.Equal("quay.io", ImageRef.Parse("quay.io/keycloak/keycloak").Registry);
    }
}
```

`tests/DockerCity.Domain.Tests/PortMappingTests.cs`:

```csharp
using DockerCity.Domain.Values;

namespace DockerCity.Domain.Tests;

public class PortMappingTests
{
    [Fact]
    public void Tekil_eslemede_host_ve_kap_portu_ayrisir()
    {
        var port = PortMapping.Parse("5050:80");

        Assert.Equal(5050, port.HostStart);
        Assert.Equal(80, port.ContainerStart);
        Assert.False(port.IsRange);
        Assert.True(port.IsPublished);
    }

    [Fact]
    public void Aralik_eslemesi_dogru_okunur()
    {
        var port = PortMapping.Parse("21000-21010:21000-21010");

        Assert.True(port.IsRange);
        Assert.Equal(21000, port.HostStart);
        Assert.Equal(21010, port.HostEnd);
        Assert.Equal(11, port.ContainerEnd - port.ContainerStart + 1);
    }

    [Fact]
    public void Yayinlanmamis_port_isaretlenir()
    {
        var port = PortMapping.Parse("6379");

        Assert.False(port.IsPublished);
        Assert.Null(port.HostStart);
        Assert.Equal(6379, port.ContainerStart);
    }

    [Fact]
    public void Protokol_eki_ayiklanir()
    {
        Assert.Equal("udp", PortMapping.Parse("53:53/udp").Protocol);
        Assert.Equal("tcp", PortMapping.Parse("53:53").Protocol);
    }
}
```

`tests/DockerCity.Parsing.Tests/CityMapBuilderTests.cs`:

```csharp
using DockerCity.Domain;
using DockerCity.Domain.Services;
using DockerCity.Parsing.Compose;
using DockerCity.Parsing.Mapping;

namespace DockerCity.Parsing.Tests;

public class CityMapBuilderTests
{
    private static CityMap Sample()
    {
        var file = new ComposeFileReader().ReadFromFile(
            Path.Combine(AppContext.BaseDirectory, "Fixtures", "docker-compose.yml"));

        var builder = new CityMapBuilder(
            new ServiceFactory(new InMemoryServiceCategoryResolver()));

        return builder.Build(file);
    }

    [Fact]
    public void Butun_servisler_haritaya_girer()
    {
        Assert.Equal(10, Sample().Services.Count);
    }

    [Fact]
    public void Servisler_dogru_alt_tipe_esler()
    {
        var map = Sample();

        Assert.IsType<DatabaseService>(map.Find("postgres"));
        Assert.IsType<DatabaseService>(map.Find("redis"));
        Assert.IsType<MessagingService>(map.Find("rabbitmq"));
        Assert.IsType<StorageService>(map.Find("minio"));
        Assert.IsType<IdentityService>(map.Find("keycloak"));
        Assert.IsType<ToolingService>(map.Find("pgadmin"));
    }

    // --- Kenar durum 1: örtük default ağı ---

    [Fact]
    public void Iki_mahalle_olusur_biri_ortuk()
    {
        var districts = Sample().Districts;

        Assert.Equal(2, districts.Count);
        Assert.False(districts.Single(d => d.Name == "fnp-network").IsImplicit);
        Assert.True(districts.Single(d => d.Name == "default").IsImplicit);
    }

    [Fact]
    public void Agsiz_servisler_ortuk_mahalleye_girer()
    {
        var implicitDistrict = Sample().Districts.Single(d => d.IsImplicit);

        Assert.Equal(
            ["keycloak", "minio"],
            implicitDistrict.Members.Select(m => m.Name).OrderBy(n => n));
    }

    [Fact]
    public void Ortuk_uyelik_yml_deki_bosluktan_ayirt_edilebilir()
    {
        var keycloak = Sample().Find("keycloak")!;

        // Dosyada yazmıyor...
        Assert.Empty(keycloak.NetworkNames);

        // ...ama anlamı gereği default mahallede.
        Assert.Contains(
            Sample().Districts.Single(d => d.IsImplicit).Members,
            member => member.Name == "keycloak");
    }

    [Fact]
    public void Fnp_network_sekiz_servis_barindirir()
    {
        Assert.Equal(8, Sample().Districts.Single(d => d.Name == "fnp-network").Members.Count);
    }

    // --- İlişkiler ---

    [Fact]
    public void Depends_on_iliskisi_uretilir()
    {
        var link = Assert.Single(Sample().Links, l => l.Kind == CityLinkKind.DependsOn);
        Assert.Equal("pgadmin", link.From);
        Assert.Equal("postgres", link.To);
    }

    [Fact]
    public void Paylasilan_volume_yoksa_iliski_uretilmez()
    {
        // Örnek dosyada her named volume tek bir servise ait.
        Assert.DoesNotContain(Sample().Links, l => l.Kind == CityLinkKind.SharedVolume);
    }

    [Fact]
    public void Paylasilan_volume_iliskisi_uretilir()
    {
        const string yaml = """
            services:
              writer:
                image: alpine
                volumes:
                  - shared_data:/data
              reader:
                image: alpine
                volumes:
                  - shared_data:/data
            volumes:
              shared_data:
            """;

        var file = new ComposeFileReader().ReadFromText(yaml);
        var map = new CityMapBuilder(
            new ServiceFactory(new InMemoryServiceCategoryResolver())).Build(file);

        var link = Assert.Single(map.Links, l => l.Kind == CityLinkKind.SharedVolume);
        Assert.Equal("reader", link.From);
        Assert.Equal("writer", link.To);
    }

    // --- Davranışlar ---

    [Fact]
    public void Rabbitmq_broker_ve_yonetim_portlarini_ayirir()
    {
        var rabbit = Assert.IsType<MessagingService>(Sample().Find("rabbitmq"));

        Assert.Equal(5672, rabbit.BrokerPort!.ContainerStart);
        Assert.Equal(15672, rabbit.WebPort!.ContainerStart);
        Assert.Equal("http://localhost:15672/", rabbit.WebUrl!.ToString());
    }

    [Fact]
    public void Kalici_veri_tespit_edilir()
    {
        Assert.True(Assert.IsType<DatabaseService>(Sample().Find("postgres")).HasPersistentData);
        Assert.False(Assert.IsType<DatabaseService>(Sample().Find("redis")).HasPersistentData);
    }

    [Fact]
    public void Nobetci_servis_isaretlenir()
    {
        Assert.True(Sample().Find("qdrant")!.IsSupervised);
        Assert.False(Sample().Find("redis")!.IsSupervised);
    }

    [Fact]
    public void Tanimsiz_bagimlilik_hata_verir()
    {
        const string yaml = """
            services:
              web:
                image: nginx
                depends_on:
                  - yok-boyle-bir-servis
            """;

        var file = new ComposeFileReader().ReadFromText(yaml);
        var builder = new CityMapBuilder(
            new ServiceFactory(new InMemoryServiceCategoryResolver()));

        Assert.Throws<InvalidOperationException>(() => builder.Build(file));
    }
}
```

`tests/DockerCity.Parsing.Tests` projesine Domain referansı gerekiyor:

```powershell
dotnet add tests\DockerCity.Parsing.Tests reference src\DockerCity.Domain
dotnet test
```

---

## 4. Takıldığın yerler

**`ComposeServiceDto` Domain'den görünmüyor.**
Doğru davranış bu. `ServiceFactory` ve `CityMapBuilder` `DockerCity.Parsing/Mapping/` altında olmalı. Bkz. bölüm 2.1.

**`CS0266: Cannot implicitly convert IReadOnlyList<T> to List<T>`**
`[.. ifade]` koleksiyon ifadesi hedef tipe göre derlenir. `IReadOnlyList<T>` bekleyen bir yere atıyorsan sorun yok; `List<T>` istiyorsan `.ToList()` kullan.

**Mahalle sayısı 2 değil 1 çıkıyor.**
`file.Networks` sözlüğünü mü dolaşıyorsun, servislerin `NetworkNames`'ini mi? İlkini dolaşman lazım — örtük ağ ikinci adımda ekleniyor.

**`fnp-network` üye sayısı 8 değil 10 çıkıyor.**
Muhtemelen `NetworkNames.Contains` yerine "ağ belirtmemişleri de ekle" gibi bir kısayol yazmışsın. Açık üyelik ile örtük üyelik ayrı adımlarda kalmalı.

**`WebUrl` testi `http://localhost:15672` bekliyorum ama `http://localhost:15672/` geliyor.**
`Uri` sonuna eğik çizgi ekler. Testte tam eşitlik yerine `WebUrl!.Port` karşılaştırmak daha sağlam.

**`warning xUnit2031` / `xUnit2029` alıyorum.**
xUnit'in analizörü `Assert.Single(collection.Where(...))` ve `Assert.Empty(collection.Where(...))` kalıplarını uyarır — filtreyi assert'in kendisine vermeni ister:

```csharp
Assert.Single(map.Links, link => link.Kind == CityLinkKind.DependsOn);
Assert.DoesNotContain(map.Links, link => link.Kind == CityLinkKind.SharedVolume);
```

Sadece stil meselesi değil: bu biçimler test düştüğünde daha iyi hata mesajı üretir. `Assert.Empty(...Where(...))` başarısız olduğunda "koleksiyon boş değil" der; `Assert.DoesNotContain` ise hangi elemanın eşleştiğini söyler.

**`Assert.Equal(["keycloak", "minio"], ...)` sırayı tutturamıyor.**
Sözlük sırası garanti değil. `OrderBy` ekle — testte yaptığım gibi.

---

## 5. Ne öğrendik

- **Bağımlılık yönü bir tasarım hatasını yakaladı.** `ServiceFactory`'nin Domain'e ait olmadığını kimse söylemedi; derleyici gösterdi. Faz 0'daki kısıtın ilk getirisi.
- **Kalıtım "ne", arayüz "ne yapabilir" sorusunu cevaplar.** Alt tipler sadece bir `Category` döndürseydi hiyerarşi gereksizdi. `BrokerPort`/`WebPort` ayrımı gibi gerçek davranış farkları kalıtımın hakkını verir.
- **Aynı gerçeği iki kez modelleme.** Mahalle üyeliği "aynı ağdalar" bilgisini zaten taşıyor; ayrıca 28 kenar üretmek senkronizasyon borcundan başka bir şey değil.
- **Dosyada yazan ile dosyanın anlamı farklı şeylerdir.** `NetworkNames` boş *ve* `District.IsImplicit` true — iki bilgi de duruyor. Parser uydurmadı, domain türetti.
- **Arayüz çıkarmak gelecekteki değişikliğin fiyatını bugünden düşürür.** `IServiceCategoryResolver` sayesinde Faz 3'te `ServiceFactory`'ye hiç dokunmayacağız.
- **Değer nesneleri kural saklar.** `ImageRef.Parse` içindeki "nokta varsa registry" kuralı tek bir yerde; her yerde `image.Split('/')` yazsaydın o kural on yere dağılırdı.

---

## 6. Kendin dene

1. **`GenericService` ne zaman devreye giriyor?** Fixture'a `image: hashicorp/vault` ekle. Hangi sınıf üretiliyor? `InMemoryServiceCategoryResolver`'a bir satır ekleyip `IdentityService`'e yönlendir. Bu tabloyu Faz 3'te veritabanına taşıyacağız — şimdi elle eklemenin ne kadar sürdüğünü ölç, karşılaştırman için.

2. **`:latest` uyarısı ekle.** `ImageRef.IsLatest` hazır ama kimse kullanmıyor. `CityMap`'e `ServicesUsingLatestTag` diye bir özellik ekle ve testle doğrula. Kaç servis çıkıyor? Üretimde bu neden bir sorun?

3. **Döngüsel bağımlılık.** `a → b → c → a` şeklinde `depends_on` içeren bir yml yaz. `CityMapBuilder` ne yapıyor? Şu anda sessizce kabul ediyor. Döngüyü tespit edip hata veren bir kontrol ekle. *(İpucu: derinlik öncelikli arama, gri/siyah düğüm işaretlemesi.)*

4. **Bir servis iki ağda olursa?** Fixture'a ikinci bir network ve ona da bağlı bir servis ekle. `Districts` ne döndürüyor? Servis iki mahallenin de üyesi oluyor mu? Faz 5'te bu durumu görsel olarak nasıl çizeceğimizi düşünmeye başla — örtüşen bölgeler kolay bir problem değil.

5. **`Describe()` yerine `ToString()` olsaydı ne değişirdi?** Bir `DatabaseService` nesnesini hata ayıklayıcıda incele. `Describe()` ayrı dururken hata ayıklayıcı ne gösteriyor? İkisini birleştirseydin bu ne olurdu?

---

**Sonraki bölüm:** `03-sqlite-ef-core.md` — `InMemoryServiceCategoryResolver`'ı emekliye ayırıp yerine EF Core + SQLite destekli bir çözücü koyacağız. Image → ikon eşlemeleri veritabanına taşınacak, `ServiceFactory`'ye tek satır dokunmadan. Adım 6'da arayüzü ayırmamızın sebebini orada göreceksin.
