# Kullanıcı Senaryoları (User Stories)

Bu dokümanda projenin genel kullanıcı senaryoları yer alır.

## Arkadaşlar Modülü

Bu modülde sisteme arkadaşlarımı ekleyebilir, bilgilerini güncelleyebilir, listeyebilirim.

### Yeni Arkadaş Ekleme

`Platform Kullnıcısı` olarak sisteme yeni bir arkadaş ekleyebilirim. Arkadaşımın `adını`, `soyadını`, `doğum tarihini`, onunla ilgili kısa `not`larımı paylaşıp kaydederim. Ardından arkadaşımla ilgili iletişim bilgilerini kaydedeceğim sayfaya geçerim. Bu sayfada arkadaşıma ait iletişim bilgilerini kaydedebilirim.

### Arkadaş Listeleme

`Platform Kullacınısı` olarak sisteme eklediğim arkadaşlarımı listeyebilir, bilgilerini bu sayfada güncelleyebilir, isimlerine göre, doğum tarihlerine göre sıralayabilirim.

## Alarm Modülü

`Platform` da tanımlı `Arkadaşlarım` için `Alarm` tanımlayabildiğim, silebildiğim, pasife çekebildiğim veya değiştirebildiğim modüldür.

### Yeni Alarm Kurma

`Platform Kullanıcısı` olarak menüdeki `Alarmlar` sayfasına geçiş yaparım. Bu sayfada `Yeni Alarm Kur` veya `Listele` seçeneklerini kullanabilirim. `Yeni Alarm Kur` sayfasında önce `Arkadaş Ara` ile alarm kurmak istediğim arkadaşımı bulurum. Ardından `Alarm Kriteri` seçimini yaparım. `Alarm Kriteri` seçimine göre `Alarm Zamanı` ve `Bildirim Yöntemini` seçerim. Son olarak `Alarm Notumu`, `75` karakteri geçmeyecek şekilde yazıp kaydederim.

- **Alarm Kriteri** : Sisteme önceden girilmiş seçeneklerdir. Şunlardan birisi olabilir.
  - Doğum Günü Hatırlatıcısı
  - Randevu Hatırlatıcısı
  - 'Şu Notumu İlet' Hatırlatıcısı
- **Alarm Zamanı** : Bildirimin gönderileceği tarih ve saati belirler.
- **Bildirm Yöntemi** : Bildirimin nasıl yapılacağının seçildiği kısımdır. Aşağıdaki seçeneklerden birisi olabilir.
  - E-Posta Gönder
  - SMS Gönder
  - Bildirim Göster

### Alarmları Listeme

`Platform kullanıcısı olarak`, `Alarmlar` sayfasından tanımlanmış tüm alarmları görebilirim. Alarmları tarihlerine göre sıralayabilir, içinde belli kelime geçen notlarına göre filtreleyebilirim. `

> `Alarm Listeleme` sayfasında alarm bilgileri anında güncellenebilir veya silinebilir.