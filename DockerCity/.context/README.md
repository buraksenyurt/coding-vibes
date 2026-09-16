# DockerCity — Baglam

Bu klasor normalde projeyi yonlendiren spec dokumanini tutar. DockerCity'de spec tek bir dosya degil, **faz faz ilerleyen bir dokuman serisi** ve baska bir repoda yasiyor:

```
C:\Users\burak\Development\ideas-pool\dockercity\docs\
├── PLAN.md                       mimari, veri modeli, 11 fazlik yol haritasi
└── tutorial/
    ├── 00-kurulum-ve-iskelet.md
    ├── 01-yaml-parser.md
    ├── 02-domain-modeli.md
    └── ...
```

Ayrim bilincli: `ideas-pool` fikirlerin ve anlatinin yeri, `coding-vibes` kodun yeri. Dokumanlar burada kopyalanmadi ki iki surum birbirinden ayrismasin.

## Ornek girdi

Parser ve domain testleri su dosyayi fixture olarak kullanir:

`tests/DockerCity.Parsing.Tests/Fixtures/docker-compose.yml`

10 servis, 4 volume, 1 acik network. Ozellikle secilmis yedi kenar durum barindirir — en onemlisi `keycloak` ve `minio`'nun hicbir network'e bagli olmamasi, yani Compose'un ortuk `default` agina girmeleri.
