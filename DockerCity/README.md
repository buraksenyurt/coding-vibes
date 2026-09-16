# DockerCity

Bir `docker-compose.yml` dosyasindaki servisleri ve aralarindaki iliskileri **oyunlastirilmis bir sehir haritasi** olarak gosteren Windows masaustu uygulamasi.

Servisler sehrin sakinleri, network'ler mahalleler, `depends_on` iliskileri aralarindaki yollar. Bir figurun uzerine gelindiginde image adi, container adi, portlar ve volume bilgileri balon pencerede acilir.

Diger calismalardan farki: bu proje tek seferde bir modele yazdirilmadi. **Faz faz ilerleyen bir ogreti** olarak kurgulandi; her asamada once dokuman yazildi, sonra kod ona gore gelistirildi. Amac calisan araci uretmek kadar WinUI 3, YamlDotNet, OOP modelleme ve EF Core konularinda adim adim ilerleyen bir malzeme birakmak.

## Teknoloji

.NET 10 · WinUI 3 (Windows App SDK) · YamlDotNet · EF Core + SQLite · CommunityToolkit.Mvvm · xUnit

## Durum

| Faz | Konu | Durum |
| --- | --- | --- |
| 0 | Solution iskeleti, calisan WinUI 3 penceresi | Tamamlandi |
| 1 | YamlDotNet ile compose okuyucu, polimorfik alan converter'lari | Tamamlandi |
| 2 | Domain modeli, kalitim hiyerarsisi, `CityMap` | Tamamlandi |
| 3 | EF Core + SQLite, image -> ikon eslemeleri | Siradaki |
| 4 | Canvas uzerinde ilk gorsellestirme (MVVM) | |
| 5 | Mahalle sinirlari | |
| 6 | Etkilesim, surukle-birak, konum kaydi (MVP) | |
| 7-10 | Baglanti oklari, zoom/pan, kullanilabilirlik, canli Docker | |

Testler: 54 (Domain + Parsing).

## Klasor yapisi

```
DockerCity/
├── DockerCity.slnx
├── Directory.Build.props
├── src/
│   ├── DockerCity.Domain/     entity'ler, deger nesneleri, CityMap   (net10.0)
│   ├── DockerCity.Parsing/    YamlDotNet DTO'lari + domain'e mapper  (net10.0)
│   ├── DockerCity.Data/       EF Core + SQLite                       (net10.0)
│   └── DockerCity.App/        WinUI 3 arayuzu          (net10.0-windows...)
└── tests/
    ├── DockerCity.Domain.Tests/
    └── DockerCity.Parsing.Tests/
```

Cekirdek uc katman platformdan bagimsiz `net10.0` hedefler; yalnizca `DockerCity.App` Windows'a baglidir. Boylece UI tiplerinin is mantigina sizmasi derleme zamaninda engellenir.

## Calistirma

```powershell
dotnet build
dotnet test
dotnet build src\DockerCity.App\DockerCity.App.csproj -p:Platform=x64
```

Visual Studio'da `DockerCity.App` baslangic projesi yapilip **DockerCity.App (Unpackaged)** profiliyle calistirilir.

## Dokumanlar

Plan ve faz faz ogreti dokumanlari ayri bir repoda tutuluyor:

`ideas-pool/dockercity/docs/` — `PLAN.md` ve `tutorial/00..NN-*.md`

Bkz. [`.context/README.md`](./.context/README.md)
