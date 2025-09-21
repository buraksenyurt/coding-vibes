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
- Proje, **src** klasörü altında oluşturulmalıdır.

## Model Nesneleri

- **Term**: Uygulama domain'ine ait kelime veya terim.
  - Özellikler:
    - **Id (int, primary key):** Benzersiz tanımlayıcı.
    - **Name (string, benzersiz):** Terim adı. Örneğin "Customer","Order Number","Invoice Date", vb.
    - **Definition (string):** Terim tanımı. Maksimum 1000 karakter.
    - **IsApproved (bool):** Onay durumu. Varsayılan olarak `false`.
    - **CreatedAt (DateTime):** Oluşturulma tarihi. Otomatik olarak atanır.
    - **UpdatedAt (DateTime):** Güncellenme tarihi. Kayıd güncellendiğinde otomatik olarak güncellenir.
    - **CreatedBy (string):** Oluşturan kullanıcı. `DomainEditor` rolünde olan kullanıcı adıdır.
    - **ApprovedBy (string, nullable):** Onaylayan kullanıcı. `DomainApprover` rolünde olan kullanıcı adıdır.
    - **ApprovedAt (DateTime, nullable):** Onaylanma tarihi. Onaylandığında otomatik olarak atanır.
    - **Version (int):** Sürüm numarası. *(Tablo kaydı güncellendiğinde otomatik olarak artar)*

Örnek Veriler:

| Id  | Name          | Definition                          | IsApproved | CreatedAt           | UpdatedAt           | CreatedBy | ApprovedBy | ApprovedAt | Version |
| --- | ------------- | ---------------------------------------------------------------------- | ---------- | ------------------- | ------------------- | --------- | ---------- | ---------- | ------- |
| 1   | Customer      | A person or organization that buys goods or services from a business. | true       | 2025-09-01 10:00:00 | 2025-09-01 10:00:00 | admin     | admin      | 2025-09-01 10:05:00 | 1       |
| 2   | Order Number  | A unique identifier assigned to each order placed by a customer. | false      | 2025-09-01 11:00:00 | 2025-09-01 11:00:00 | user1    | null       | null       | 1       |

## Temel Fonksiyonlar

- **Terim Ekleme**: Kullanıcı, yeni bir terim ekleyebilir. Eklenen terim varsayılan olarak onaysızdır.
- **Terim Onaylama**: Yetkili kullanıcı, onaylanmamış terimleri onaylayabilir.
- **Terim Listeleme**: Kullanıcı, eklediği terimleri listeleyebilir, güncelleyebilir veya silebilir. *(Onaylanmış terimler silinemez veya güncellenemez)*

## Geliştirme Aşamaları

Projenin `1.0.0.0` sürümünde olması istenenler:

- Kullanıcı ve rol yönetimi sistemi olmadan, 
  - `Terim Ekleme` fonksiyonelliğinin çalışır hale getirilmesi.
  - `Terim Listeleme` sayfasında eklenen terimlerin görüntülenmesi, filtrelenmesi ve sıralanması.
  - `Terim Listeleme` sayfasında silme ve güncelleme işlemlerinin yapılabilmesi.
