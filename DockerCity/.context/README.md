# DockerCity — Bağlam

Bu klasör normalde projeyi yönlendiren spec dokümanını tutar. DockerCity'de spec tek bir dosya değil, **faz faz ilerleyen bir doküman serisi** ve başka bir repoda yaşıyor:

```
C:\Users\burak\Development\ideas-pool\dockercity\docs\
├── PLAN.md                       mimari, veri modeli, 11 fazlık yol haritası
└── tutorial/
    ├── 00-kurulum-ve-iskelet.md
    ├── 01-yaml-parser.md
    ├── 02-domain-modeli.md
    ├── 03-sqlite-ef-core.md
    ├── 04-canvas-ve-mvvm.md
    └── ...
```

Ayrım bilinçli: `ideas-pool` fikirlerin ve anlatının yeri, `coding-vibes` kodun yeri. Dokümanlar buraya kopyalanmadı ki iki sürüm birbirinden ayrışmasın.

## Örnek girdi

Parser, domain ve yerleşim testleri şu dosyayı fixture olarak kullanır:

`tests/DockerCity.Parsing.Tests/Fixtures/docker-compose.yml`

10 servis, 4 volume, 1 açık network. Özellikle seçilmiş yedi kenar durum barındırır — en önemlisi `keycloak` ve `minio`'nun hiçbir network'e bağlı olmaması, yani Compose'un örtük `default` ağına girmeleri.

## Her fazın bıraktığı iz

| Faz | Kodda karşılığı |
| --- | --- |
| 0 | `DockerCity.slnx`, `Directory.Build.props` — bağımlılık yönü proje referanslarıyla sabitlendi |
| 1 | `DockerCity.Parsing/Compose`, `Converters`, `Dto` — YAML'ın polimorfik alanları |
| 2 | `DockerCity.Domain` — abstract taban, yetenek arayüzleri, `CityMap` |
| 3 | `DockerCity.Data` — EF Core, dört tablo, tohumlanmış image kataloğu |
| 4 | `DockerCity.App/ViewModels`, `Controls/CityCanvas`, `Domain/Layout` |
