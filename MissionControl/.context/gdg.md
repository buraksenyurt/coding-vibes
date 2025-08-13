# Mission Control - Game Design Document

`Mission Control` , klasik `Space Invaders` türevli bir uzaylı istilası önleme oyunudur.

## Senaryo

`Dünya` ve insanlığın geleceği bu son savunma hattına bağlıdır. `Captian Gadot` dünyaya yaklaşmakta olan uzaylı gemilerini imha etmek için `Silisyum ile güçlendirilmiş Donut Gun` başına geçmiştir. Uzaylıların en büyük problemi `Dünyalı Tatlı Çöreklerini` çok sevmeleridir. Arka arkayya **3 donut yediklerinde** şişerek patlarlar. İçinde bulundukları istila gemileri de bu nedenle düşer.

## Oynanış

Oyun başladıktan sonra ekranın üst kısmındaki rastgele konunlardan `Invader` lar dikey olarak inmeye başlar. `Captian Gadot`' un tankı ekranın taban kısmında sağa ve sola doğru hareket edebilir. Hareket için `A` ve `D` veya `Left`, `Right` tuşları kullanılır. Yönler ters çalışır. `Left` tuşuna basıldığında sola gitmek yerine sağa gidilir, `Right` tuşuna basıldığında sağa gitmek yerine sola gidir.

`Captain Gadot`'un tankı saniyede 1 donut fırlatabilir. Donut'lar dikey yönde, tankın dikey namlusunda ekranın üst kısmına doğru ateşlenir. Bir `Invader Ship` 3 isabet aldığında patlar. Zemine ulaşan her bir `Invader Ship`, 5 ile 10 arasında `Donut Lover` değişen sayılarda personeli dünyaya bırakmış olur. Toplamda 100 `Donut Lover`' ın dünyaya ulaşması oyunun kaybedilmesi anlamına gelir.

Oyun süresinde rastgele anlarda nadiren ve periyotlar arası en az 15 saniye olması şartıyla Dünya'ya `Spatula` isimli dörtgenler düşer. `Captain Gadot` bunları yakaladığı andan itibaren 10 saniye boyunca `Seri atış` yapabilir. `Seri atış`, 250 milisaniyede 1 Donut gönderilebilmesi demektir.

Oyuncunun herhangibir anda kullanabileceği tek atımlık `Electromagnetic Donuts Bomb` isimli bir süper silahı da vardır. `Space` tuşuna basıldığında tetiklenir ve her oyunda sadece 1 kere kullanılabilir. Oyun sahasında zamanın 10 saniye boyunca durmasını sağlar. Bu süre zarfında `Captain Gadot` tankını hareket ettirebilir, ateş edebilir ve `Invader Ship`'ler isabet alabilir ancak tank dışında hiçbir nesne hareketine devam etmez.

## Oyunun Kuralları

- Uzaylı istilası 3 dakika ile sınırlıdır.
- `Invader Ship` gemilerinden 10 tanesinin zemine ulaşması yani toplamda 100 `Donut Lover`'ın dünyaya inmesi halinde oyun kaybedilir.
- Başlangıçta oyuncunun 100 puanı vardır. Dünyaya inen her `Invader Ship`' e karşılık bu puan `Invader Ship` içerisindeki asker sayısı kadar azalır.
- 3 dakika içerisinde düşürülen her `Invader Ship` için oyuncu 10 puan kazanır.
- `Captain Gadot` ' un tankı saniyede bir `Donut` fırlatır ancak `Spatula` bonuslarını yakaladığında `250 milisaniyede 1 Donut` ateşleyebilir.
- Bir `Invader Ship` gemisi toplamda 3 isabet aldığında patlar ve yok olur.
- Oyun sahasında aynı anda en fazla 5 `Invader Ship` olabilir.
- Oyunca her oyunda sadece 1 kez `Electromagnetic Donuts Bomb` kullanabilir. `Electromagnetic Donuts Bomb` sadece 10 saniye aktfi kalabilir. `Space` tuşuna basılmasıyla tetiklenir.

## Aktörler

- **Invader Ship:** Uzaylı istila gemisidir. Üç farklı boyutta dikdörtgen formundadır. Dikey ivmeleri `saniyede 1 kare`, `1 Kare/750ms` ve `1 kare/500ms` şeklindedir. Ekranın üst kısmından rastgele konumlardan, belirtilen hızlardan birisi ile giriş yaparlar. Her `Invader Ship` içinde 5 veya 10 asker olur. Bu sayı rastgele belirlenir.
- **Captain Gadot:** Oyuncunun kendisidir. Tankı kullanır.
- **Tank:** Line based vektör olarak çizilmiş bir tank formundadır. Tank topu ekrana diktir ve yukarı yönlü ateş edecek pozisyondadır. Tank `Saniyede 1 kare` hızında yalnızca sola veya sağa doğru hareket eder.
- **Donut:** Tank mermisidir. İçiçe iki daire ile çizilmiş basit bir donut formundadır. Büyüklüğü `Tank` ve `Invader Ship` ile orantılıdır.
- **Donut Lover:** `Invader Ship` içerisindeki askerleri temsil eder. Her `Invader Ship` içerisinde maksimum 10 `Donut Lover` olabilir.

## PowerUps

- **Spatula:** `Captain Gadot`' un `Tank`'ı ile seri atış yapmasını sağlayan bonustur. İçinde `S` harfi yazan bir kare şeklindedir.
- **Electromagnetic Donuts Bomb:** Her oyunda sadece 1 kere kullanılabilen, oyun zamanını 10 saniye durduran güçlendirmedir.

## Ekranlar

Oyun 4 farklı ekrandan oluşur. `Enter` tuşu seçim yapılmasında, `Esc` tuşu bir önceki ekrana dönülmesinde veya oyundan çıkılmasında kullanılır.

### Menu

Oyun klasik bir menü ile başlar. Menü seçenekleri arasında yukarı aşağı yön tuşları ile hareket edilebilir. Enter tuşuna basıldığında oyun başlar. Menüler şöyledir;

- **Start:** Oyunu başlatır.
- **Story:** Oyunun hikayesinin gösterildiği ekrana gidilir.
- **Scores:** Son 5 oyun skoru bilgisi ile şu ana kadar alınmış en yüksek skor bilgisini gösteren skor ekranını açar.
- **Exit:** Oyundan çıkılır.

### Oyun Ekranı

Oyun ekranı dikeyde üç bloktan oluşur.

- **HeadsUp Display:** Yukarıdan aşağıya 50 pixel yüksekliğindeki kısmın üzerinde dünyaya ulaşmış `Donut Lover` sayısı, oyuncunun anlık puanı ve 3 dakikadan geriye doğru sayan kronometre yer alır.
- **Game Schene:** Oyunun oynandığı alandır. 600 Pixel yüksekliğindedir.

### Senaryo Ekranı

Bu ekranda dokümanın `Senaryo` kısmında yazan hikaye AI tarafından zenginleştirilerek sunulur.

### Skorlar(Scores) Ekranı

Skorlar ekranında son 5 oyuna ait skor bilgileri ve tamamlanma süreleri yer alır. Skorlar `game.dat` isimli dosyada aşağıdaki örnek JSON formatında tutulur.

```json
{ "best": number, "history": [number, ...max5] }
```

### Oyun Sonu Ekranı

Oyun oynanırken galibiyet veya yenilgi hallerinde çıkan ekrandır. Bu ekranda oyuncunun puanı, yer yüzüne inen toplam `Donut Lover` sayısı, yenilgi hallerinde oyunda kalınabilen süre bilgileri yer alır.

## UI Akışı ve Durumlar(States)

- **Global State:** MainMenu → Story → Game → GameOver → Scores → MainMenu *(Esc ile geri)*.
- **HUD:** elapsed/remaining time, score, landed lovers, bomb status *(kullanıldı/kullanılmadı ikon)*, rapid fire kalan süre barı.
- **Scores:** son 5 oyun skoru ve `en iyi oyun skoru`.

## Grafik

Oyun karakterleri line based basit vektör grafiklerden oluşur. Ölçek bazında taslaklar aşağıdaki gibidir.

### Invader Ship

Small;

```text
╭─╮
│ │
╰─╯
```

Midi;

```text
╭──╮
│  │
╰──╯
```

Large;

```text
╭─────╮
╰─────╯
```

### Tank

```text
 ╭╮ 
╭┴┴╮
╰──╯ 
```

### Spatula

```text
╭───╮
│ S │
╰───╯
```

### Donut

```text
◉
```

## Teknoloji

Oyun kodları `Rust` programlama dili kullanılarak, `Windows 11` sistemde geliştirilir. ECS _(Entity Component System)_ tabanlı `Bevy` oyun motorunun `0.16` sürümü kullanılarak geliştirilir. Tüm Asset'ler vektör grafiklerden oluşur.

## Ekran

500 pixel genişlik ve 650 piksel yüksekliğe sahiptir.

## Minimum Sistem Konfigurasyonu

- OS: Windows 11 22H2+
- CPU: 2+ çekirdek
- RAM: 4 GB
- GPU: Entegre GPU yeterli (Vulkan/DirectX 12 uyumlu)
- Depolama: <50 MB
