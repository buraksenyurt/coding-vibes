# Friendsly Proje Dokümantasyonu

Friendslyvakıllı bir rehber uygulamasıdır. Bu uygulama sayesinde kullanıcılar arkadaşlarını kaydedebilir, bilgilerini yönetebilir, alarm bildirimleri kurabilir ve "darıldım" işleviyle ilişki durumlarını takip edebilir.

## Kullanılacak Teknikler

- .NET 9.0 ile Clean Architecture implementasyonu yapılır.
- DDD prensiplerine uygun bir alt yapı içerir.
- Entity Framework Core ile PostgreSQL veritabanı kullanılır.
- Komut ve sorgular MediatR ile yönetilir.
- Sol kenar çubuğu navigasyonlu profesyonel responsive web arayüzü.
- Frontend ve backedn iletişimi RESTful API'ler üzerinden sağlanmalıdır.
- Frontend tarafta özel CSS kullanılmaz, Bootstrap ve FontAwesome kütüphaneleri kullanılır.

## Ana Varlıklar (Entities)

### Person (Kişi)

Rehberdeki bireyleri temsil eden ana varlık.

**Özellikler:**

- Kişisel Bilgiler: FirstName, LastName, BirthDate, About, Notes
- Darıldım Durumu: IsOffended, OffendedReason, OffendedAt

### Detail (Detay)

Kişilere bağlı iletişim bilgisi girdilerini tutar.

**Özellikler:**

- Type: ContactType enum (Phone, Email, Address, SocialMedia)
- Value: Gerçek iletişim bilgisi
- Label: İsteğe bağlı açıklayıcı etiket
- AdditionalInfo: Ek bilgi
- IsPrimary: Birincil iletişim yöntemi olarak belirleme

### Alarm (Alarm)

Kişi ile ilgili hatırlatmalar için bildirim sistemine ait verileri taşır.

**Özellikler:**

- Name, Description: Alarm tanımlaması
- Criteria: AlarmCriteria enum (Birthday, Appointment, Memo)
- TriggerDate: Alarmın ne zaman aktif olacağı
- DaysBefore: Önceden bildirim süresi
- IsActive: Etkinleştir/devre dışı bırak durumu
- IsRecurring: Tekrarlanan alarm
- Actions: Bildirim aksiyonları koleksiyonu

### Action (Aksiyon)

Alarmlar için spesifik bildirim yöntemlerini tanımlar.

**Özellikler:**

- Type: ActionType enum (Email, SMS, Notification)
- Message: Bildirim mesajı
- IsExecuted: Yürütülme durumu
- ExecutedAt: Yürütülme zamanı

## Veritabanı Konfigürasyonu

Uygulama veritabanı olarak Postgresql kullanır.

### PostgreSQL Kurulumu

Veritabanı aşağıdaki konfigürasyonla Docker konteyner üzerinde çalışır.

```yaml
services:
  postgres:
    image: postgres:latest
    container_name: plh-postgres
    environment:
      POSTGRES_USER: johndoe
      POSTGRES_PASSWORD: somew0rds
      POSTGRES_DB: postgres
    ports:
      - "5432:5432"
    volumes:
      - postgres_data:/var/lib/postgres/data

  pgadmin:
    image: dpage/pgadmin4:latest
    container_name: plh-pgadmin
    environment:
      PGADMIN_DEFAULT_EMAIL: scoth@tiger.com
      PGADMIN_DEFAULT_PASSWORD: 123456
    ports:
      - "5050:80"
    depends_on:
      - postgres
```

> Docker Compose servisleri zaten çalışıyor. Sadece migration yürütülmesi gerekli.

### Entity Framework Konfigürasyonu

O/RM aracı olarak Entity Framework kullanılır. Genel özellikler şöyledir.

- **Code-First Yaklaşım**: Domain modellerinden oluşturulan veritabanı şeması kullanılır.
- **Migration Yönetimi**: EF migration'ları aracılığıyla otomatik veritabanı güncellemeleri yapılır.
