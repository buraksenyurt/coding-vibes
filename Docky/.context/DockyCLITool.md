# Docky CLI Tool

C# ile geliştirilmiş, kolayca docker-compose dosyaları oluşturulmasını sağlayan bir .net tool'dur.

## Giriş

.Net Tool'lar, .Net CLI *(Command Line Interface)* üzerinden kullanılabilen komut satırı araçlarıdır. Kendi özel .Net Tool'umuzu yazarak, belirli görevleri otomatikleştirebilir veya geliştirme süreçlerinizi kolaylaştırabiliriz. Bu araçlar sisteme `global` veya `local` olarak yüklenebilir ve kullanılabilir. `

Örneğin `Entity Framework` işlerini komut satırından gerçekleştirebildiğimi `ef` bu araçlardan birisidir. Aşağıda bu aracın sisteme yüklenmesi ve kullanımı ile ilgili örnekler yer almaktadır.

```bash
# Dotnet EF Tool'unu Yükleme
# global olarak yüklemek için:
dotnet tool install --global dotnet-ef
# local olarak yüklemek için:
dotnet tool install --local dotnet-ef

# Dotnet EF Tool'unu Kullanma
# Örneğin, bir Entity Framework Core migration eklemek için:
dotnet ef migrations add InitialCreate

# Migration'ları uygulamak için:
dotnet ef database update

# Mevcut migration'ları listelemek için:
dotnet ef migrations list
```

## Proje Kriterleri

Geliştirilmesi planlanan `CLI` aracının genel özellikleri aşağıdaki gibidir:

- Proje adı `Docky`dir.
- Proje `.Net 9.0` platformunda `C#` ile geliştirilir.
- UX zenginliği için `Spectre.Console` kütüphanesi kullanılır.
- Program belli yazılım geliştirme modelleri için otomatik olarak bir `docker-compose` dosyası hazırlar.
- Kullanılabilecek modeller parametre olarak verilir. Bu modeller aşağıdaki gibi olabilir:
  - `base`: PostgreSQL, PgAdmin servislerini içeren `docker-compose` dosyası oluşturur.
  - `full`: PostgreSQL, PgAdmin, Elasticsearch, Redis, RabbitMQ, Kibana ve Sonarqube servislerini içeren bir `docker-compose` dosyası oluşturur.

## Kullanım Örnekleri

Uygulama `dotnet tool` ile sisteme yüklenir ve `docky` komutu ile çalıştırılır. Projenin derlenmiş hali `nupkg` klasöründe yer alır. Projeyi sisteme yüklemek için aşağıdaki komutlar kullanılır:

```bash
# Projeyi global olarak yükleme
dotnet tool install --global docky --add-source ./nupkg

# Projeyi local olarak yükleme
dotnet tool install --local docky --add-source ./nupkg

# Yüklenen tool'u güncelleme
dotnet tool update --global docky --add-source ./nupkg

# Yüklenen tool'u kaldırma
dotnet tool uninstall --global docky
```

Tool yüklendikten sonra `docky` komutu ile çalıştırılabilir. Aşağıdaki örnekler, farklı modellerde `docker-compose` dosyası oluşturmayı göstermektedir:

```bash
# Full Model türünde bir Docker Compose dosyası oluşturma
docky generate docker-compose --model full

# Base Model türünde bir Docker Compose dosyası oluşturma
docky generate docker-compose --model base

# Oluşturulan modele ek olarak Redis servisi ekleme
docky generate docker-compose --model base --add-redis

# Oluşturulan modele ek olarak RabbitMQ servisi ekleme
docky generate docker-compose --model base --add-rabbitmq

# Oluşturulan modele ek olarak Opensearch servisi ekleme
docky generate docker-compose --model base --add-opensearch

# Servisin dışarıdan erişim portunu değiştirerek ekleme
docky generate docker-compose --model base --add-redis --redis-port 6380

# Eklenen servisi içeriğini referans bir yml dosyasından okuma
# Örnekte MinIO servisi ekleniyor
docky generate docker-compose --model base --add-service /path/to/minio-service.yml
```

## Versiyon 2.0

- **Servis Desteği(DockyHub):** Docker imajlarının kod içerisinde tanımlanması yerine `Rest` tabanlı bir servis üzerinden alınması sağlanacak. Böylece `docker imajlarına` ait compose içeriklerinin güncellenmesi, yenilerinin eklenmesi kolaylaşacak.
- **Model Listesi:** Kullanılabilecek modellerin listesi için yeni bir komut eklenecek. Bu komutlar ilgili model içerisinde yer alan servisleri, kullandıkları portlar da görülebilecek.
- **Container Servis Listesi:** Özellikle -add öneki ile eklenen servislerin listesi için yeni bir komut eklenecek. Bu komut eklenebilecek servislerin listesi de görülebilecek.

### DockyHub

`DockyHub`, `Docky CLI Tool` için servis sağlayıcı görevi üstlenen bir servis. Bu servis, Docker imajlarına ait `docker-compose` içeriklerini barındırır ve Docky CLI Tool'un bu içeriklere erişmesini sağlar. `DockyHUB`, `.Net 9.0` ile geliştirilmiş bir `Web API` projesidir. `DockyHub`'ın temel özellikleri şunlardır:

- **Servis Kataloğu:** DockyHub, çeşitli Docker imajlarına ait `docker-compose` dosyalarında kullanılabilecek servis tanımlama içeriklerini barındırır. Bu içerikler, farklı servislerin nasıl yapılandırılacağını ve çalıştırılacağını tanımlar.
- **Model Kataloğu:** DockyHub, farklı yazılım geliştirme modellerine uygun `docker-compose` içeriklerini de sunar. Örneğin, `base`, `full`, `microservices` gibi modeller için önceden tanımlanmış içerikler otomatik olarak sağlar. Var olan CLI aracında yer alan modellerin tamamı servise entegre edilmelidir.
- **Repository:** Servis ve model tanımlamalarına ait yml içerikleri şimdilik `yml` uzantılı fiziki dosyalarda tutulabilir. *(Sonraki sürümde bu içeriğin `Postgresql` veritabanından okunması için gerekli geliştirmeler yapılacaktır)*
- **API Erişimi:** Docky CLI Tool, DockyHub'a REST API üzerinden erişir. Bu sayede, gerekli `docker-compose` içerikleri dinamik olarak alınabilir.
- **Güncellemeler:** `DockyHub`, yeni servislerin eklenmesi veya mevcut servislerin güncellenmesi için merkezi bir nokta sağlar. Bu, Docky CLI Tool'un her zaman en güncel içeriklere erişmesini mümkün kılar.