# DoDi Web Uygulaması

## Künye

- **Proje Adı**: DoDi
- **Proje Konusu**: DoDi, belirli bir uygulama domain'ine ait kelimelerin ve terimlerin çevrimiçi girildiği, onay mekanizması ile eklenebildiği bir platformdur. Uygulama, Domain Drive Design *(DDD)* tabanlı projelerde kullanılan Ubiquitous Language *(UL)* oluşturulmasına yardımcı olmayı amaçlar.
- **Tanım**: Uygulama domain'ine ait kelimelerin ve terimlerin çevrimiçi girilebildiği, onay mekanizması ile eklenebildiği bir web uygulaması.
- **Geliştirici**: Grok Code Fast (Preview 1)
- **Doküman Versiyonu**: 1.00
- **Oluşturulma Tarihi**: Eylül 2025

## Teknik Gereksinimler

- Proje **.Net 9.0** platformunda ve **Razor Pages** tabanlı **Web App** olarak geliştirilmelidir.
- Veritabanı olarak **PostgreSQL** kullanılmalıdır.
- Geliştirme safhasında root klasörde yer alan **docker-compose.yml** dosyası ile PostgreSQL servisi ayağa kaldırılabilir.
- Uygulama, **Entity Framework Core** kullanarak PostgreSQL ile etkileşimde bulunmalıdır.
- Uygulama basit **Model View Controller** *(MVC)* mimarisine göre geliştirilmelidir.
- Kullanıcı arayüzü, **Bootstrap 5** kullanılarak tasarlanmalıdır.

## Model Nesneleri

- **Term**: Uygulama domain'ine ait kelime veya terim.
  - Özellikler:
    - Id (int, primary key): Benzersiz tanımlayıcı.
    - Name (string, benzersiz): Terim adı. Örneğin "Customer","Order Number","Invoice Date"
    - Definition (string): Terim tanımı.
    - IsApproved (bool): Onay durumu.
    - CreatedAt (DateTime): Oluşturulma tarihi.
    - UpdatedAt (DateTime): Güncellenme tarihi.
    - CreatedBy (string): Oluşturan kullanıcı.
    - ApprovedBy (string, nullable): Onaylayan kullanıcı.
    - ApprovedAt (DateTime, nullable): Onaylanma tarihi.
    - Version (int): Sürüm numarası.

## Temel Fonksiyonlar

- **Terim Ekleme**: Kullanıcı, yeni bir terim ekleyebilir. Eklenen terim varsayılan olarak onaysızdır.
- **Terim Onaylama**: Yetkili kullanıcı, onaylanmamış terimleri onaylayabilir.
- **Terim Listeleme**: Kullanıcı, eklediği terimleri listeleyebilir, güncelleyebilir veya silebilir. (Onaylanmış terimler silinemez veya güncellenemez.)

## Beklentiler

İlk versiyon olan 1.0.0.0. Sürümünde Olması İstenenler:

- İlk sürümde kullanıcı ve rol sistemi olmadan terim ekleme fonksiyonelliğinin çalışır hale getirilmesi.
- Terim listeleme sayfasında eklenen terimlerin görüntülenmesi, filtrelenmesi ve sıralanması.