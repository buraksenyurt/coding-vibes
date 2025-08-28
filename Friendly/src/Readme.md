# Çalışma Detayları

Bu proje, kullanıcı dostu bir arayüz ve etkileşimli özellikler sunan bir uygulama geliştirmeyi amaçlamaktadır. Aşağıda projenin temel bileşenleri ve işleyişi hakkında detaylı bilgiler bulunmaktadır. Proje yapımına github codespaces üzerinden başlanmıştır. AI Agent olarak Claude Sonnet 4 sürümü kullanılmıştır.

> Projen tanıtımı ve detayları için .context klasöründeki dosyalar oluşturulmuştur.

## AI Agent'tan Beklentiler

- Kullanıcı dostu bir arayüz oluşturabilmesi.
- Kullanıcı senaryolarını olabildiğince kapsamlı bir şekilde ele alması.
- Doğru Nuget paketleri ve kütüphaneleri kullanması.
- Proje yapısını ve kod kalitesini yüksek tutması.
- Docker Container'ların Codespaces üzerinde sorunsuz çalışmasını sağlaması.
- Postgresql Migrations işlemlerini doğru bir şekilde yapması.
- Backend ve Frontend arasında API bazlı entegrasyonu sağlaması.
- Proje boyunca düzenli commitler yapması ve her commit mesajında yapılan değişiklikleri

## Konuşma Geçmişi

Projenin geliştirilmesi sırasında AI Agent ile yapılan konuşmalarda sorulan sorular kronolojik olarak aşağıda listelenmiştir:

```text
SORU: Merhaba.
Friendly|.context klasörü içerisinde geliştirmek istediğim projeye ait belgeler yer alıyor. Bu belgeleri analiz edip projeyi oluşturabilir misin? Projeyi src klasörü altında oluşturalım.

SORU: Doğru EF paketini mi kullanıyoruz?Sadece Abstraction'ının ekledik gibi. Aslında Microsoft.EntityFramework olması gerekmez mi? (EF Build Hataları Üzerine Sordum)

SORU: Devam etmeden önce root klasördeki .gitignore dosyasını .net için güncelleyebilir miyiz? Şu an gereksiz içeriklerde git tarafında görünüyor.

SORU: Web projesindeki dist klasöründekiler de gerekli mi? Değilse gitignore'a eklenebilirler mi?

SORU: Aslında sadece lib klasörünü .gitignore ile hariç tutacak şekilde güncelleme yapsak yeterliydi diye düşünüyorum. Libman kullanmak istemiyorum.

SORU: Yeni Arkadaş kaydetme kısmına aşağıdaki hatayı alıyorum.
"ArgumentException: Cannot write DateTime with Kind=Unspecified to PostgreSQL type 'timestamp with time zone', only UTC is supported. Note that it's not possible to mix DateTimes with different Kinds in an array, range, or multirange. (Parameter 'value')"

SORU: Hata devam ediyor görünüyor.
"ArgumentException: Cannot write DateTime with Kind=Unspecified to PostgreSQL type 'timestamp with time zone', only UTC is supported. Note that it's not possible to mix DateTimes with different Kinds in an array, range, or multirange. (Parameter 'value')
Npgsql.Internal.Converters.DateTimeConverterResolver<T>.Get(DateTime value, Nullable<PgTypeId> expectedPgTypeId, bool validateOnly)

DbUpdateException: An error occurred while saving the entity changes. See the inner exception for details.
Microsoft.EntityFrameworkCore.Update.ReaderModificationCommandBatch.ExecuteAsync(IRelationalConnection connection, CancellationToken cancellationToken)"

SORU: Yeni arkadaş ekleyebildim ancak sitede halen eksikler var.

- Ana sayfada MVC template'inden kalma bilgiler var. Projeye uygun bir motto içeren bir tasarım olabilir. Sol menü site navigasyonunu aşağıdaki gibi tasarlayalım.
    Kişiler
     Tüm Arkadaşlarım
     Yeni Arkadaş Ekle
     Darıldıklarım
    Alarmlar
     Tüm Alarmlar
     Yeni Alarm
    Diğer
     Ayarlar
     Gizlilik
- Halen eksik View'lar var. Arkadaş kaydettiken sonra iletişim bilgilerini girdiğimiz ekran eksik.
- Tüm arkadaşların listelendiği ekran eksik.
- Alarm modülündeki ekranlar eksik.
Bunları da tamamlar mısın?

SORU: Düzeltmeni beklediğim birkaç tasarım sorunu var.

- Ana sayfadaki
"Friendly'ye Hoş Geldin!" yazısı beyaz zemin üstünde beyaz kaldığı için görünmüyor.
- Sol üst kısımda, Kalp ikonunu kullandığın yerden veya farklı bir linkten Anasayfaya dönüş bulunmuyor.
- Anasayfa görünümünde vertical scroll çıkıyor. Sanki tasarım cihaz ekran boyutlarına adapte olmuyor.

İlk etapta bunları düzeltebilir misin?

SORU: Ana sayfa tasarımı bu sefer horizontal scroll çıkmasına sebeb oldu. Custom CSS kullanmadan, Bootstrap sınırları içerisinde kaldığımız bir tasarım ile değiştirebilir miyiz?
```

## Çalışma Zamanından Ekran Görüntüleri

// EKLENECEK