# Coding-Vibes

Özellikle CoPilot ve AI destekli neler yapılabileceğini deneyimlemeye çalıştığım örneklerin yer aldığı repodur. Olabildiğinde yeni gelen modelleri kullanarak farklı zorluk seviyelerinde programlar yazdırmaya çalışıyorum.

## Test Sistemi

| **Donanım** | **Özellik**                                                                    |
| ----------- | ------------------------------------------------------------------------------ |
| **CPU**     | 12th Gen Intel Core i7-1255U, 1700 Mhz, 10 Core(s), 12 Logical Processor(s)    |
| **RAM**     | 32Gb                                                                           |
| **VGA**     | Adapter Type Intel(R) Iris(R) Xe Graphics Family, Intel Corporation compatible |
| **OS**      | Windows 11                                                                     |
| **IDE**     | Visual Studio Code                                                             |

## Çalışmalar

<table>
<tr>
<th>Proje</th>
<th>Detaylar</th>
<th>Durum</th>
</tr>
<tr>
<td valign="top"><strong>Lunar Landing(Oyun)</strong></td>
<td valign="top"><strong>Tarih:</strong> Temmuz 2025<br><strong>Tanım:</strong> Uzay temalı 2d platform oyunu<br><strong>Geliştirici:</strong> Sonnet 4.0<br><strong>Dil:</strong> Python<br><strong>Zorluk:</strong> Orta<br><strong>Süre:</strong> ~2 Saat</td>
<td valign="top">Çalışır bir versiyon yazabildi. Python dilini üst düzey soyutlamaları işini oldukça kolaylaştırdı.</td>
</tr>
<tr>
<td valign="top"><strong>Mission Control(Oyun)</strong></td>
<td valign="top"><strong>Tarih:</strong> Ağustos 2025<br><strong>Tanım:</strong> Space Invaders benzeri 2d platform oyunu<br><strong>Geliştirici:</strong> GPT 5.0(Preview)<br><strong>Dil:</strong> Rust<br><strong>Zorluk:</strong> Zor<br><strong>Süre:</strong> -</td>
<td valign="top">GPT 5.0 Preview, ilk derlemede hatalar oluştu. Hata mesajlarını değerlendirip birkaç denemede düzeltmeye çalıştı ancak başarılı olamadı. Düzeltmeler için Sonnet 4.0'a geçildi. Öncelikle Bevy 0.16 API'sini öğrenmeye ve değişiklikleri anlamaya çalıştı. <em>(Bevy geriye uyumluluk yönünden oldukça zorlayıcı bir paket)</em> Temel çalışma mantığını anladı ve projeyi derlenebilir hale getirdi.</td>
</tr>
<tr>
<td valign="top"><strong>Friendsly(Web App)</strong></td>
<td valign="top"><strong>Tarih:</strong> Ağustos 2025<br><strong>Tanım:</strong> 90larda sıkça yazdığımız Fihrist(Telefon Rehberi) uygulamasının AI destekli modern bir versiyonu<br><strong>Geliştirici:</strong> Sonnet 4.0<br><strong>Dil:</strong> C#<br><strong>Zorluk:</strong> Orta Üstü<br><strong>Süre:</strong> ~8 Saat</td>
<td valign="top">User Story kalitesinden dolayı bazı View'lar eksik kaldı veya yanlış yazdı.<br>Entity Framework için doğru paketi bulamadı.<br>Belirttiğim gibi Web uygulaması ile backend tarafını Web API üzerinden konuşturmak yerine Web App içerisinde doğrudan EF Context nesnelerine gitti.<br>Alarm ekleme fonksiyonelliğindeki hataları düzeltemedi.</td>
</tr>
<tr>
   <td valign="top"><strong>Walkure(Oyun)</strong></td>
<td valign="top"><strong>Tarih:</strong> Ağustos 2025<br><strong>Tanım:</strong> Ücretsiz assetlerin kullanıldığı 2D Platform oyunu<br><strong>Geliştirici:</strong> Gemini 2.5 Pro<br><strong>Dil:</strong> Go<br><strong>Zorluk:</strong> Orta<br><strong>Süre:</strong> </td>
<td valign="top"></td> 
</tr>
</table>
