# Lunar Race - Game Design Document

Lunar Race isimli 2D platform oyununa ait tasarım dokümanıdır.

## Oyunun Hakkında

Uzayın sonsuz karanlığında yıldızlar arasında seyahat eden bir gezgin, kuyruklu yıldızlar, kara delikler ve göktaşları arasından sıyrılarak bir gezegenden ötekine yolculuk eder. Amacı gittiği gezegenlerden değerli minareller toplamak ve `listede yer alan görevleri tamamlayarak galaksi vatandaşlarına yardımcı olmaktır.

## Amaç

Oyun başlangıcında oyuncunun 10 bin `Spaceron` bütçesi ve 1000 `Celluron` enerjisi vardır. Oyuncu, karşılama ekranından sonra görev listene bakar ve bir `görev` seçer. Görev seçimine bağlı olarak tamamlaması gereken mineraller için gezegen listesinden gideceği ilk gezegeni seçer. Gezegen adlarının yer aldığı listeden, gezginin bulunduğu gezegene olan uzaklık, sahip olduğu mineral miktarları ve birim ücretleri bilgileri yer alır.

## Main Layout

Oyun `2D` temellidir ve sahneler yatay düzlemde hareket eder. Oyuncunun kullandığı mekik dışındaki göktaşları, gezegenler, kara delikler ve diğer tüm aktörler ekranın sağından soluna doğru hareket ederek ilerler. Aktörler ekranın sağında bir anda belirmez. Ekranın dışında oluşur kendi hızlarına göre sahneye giriş yapar ve ekranın solundan çıkarlar.

- Ekranın üst köşesindeki yatay barda toplam bütçe, görev, gidilen gezen  bilgileri yer alır.
- Ekranın alt kısmındaki yatay barda geminin güncel hızı, kalan yakıt miktarı, hedef gezegene kalan uzaklık, geminin hasar durumu bilgileri yer alır.

## Aktörler

- **Mekik:** Oyuncunun kullandığı uzay mekiğidir. Yatay düzlemde duran üçgen vektör şeklindedir. Çizgiler belirgin çekilde kalındır. Mekik ileri, geri, yukarı ve aşağı düz hareketler yapabilir.
- **Oyuncu:** Mekiği kullanır. W,S,A,D veya yön tuşları ile aracını hareket ettirir. `Space` tuşuna basarak mekiği belli süre `WARP` süratine çıkarabilir. WARP hızında sadece beş saniye gidebilir. Tekrardan WARP hızına çıkabilmesi için iticilerin şarj olmasını beklemelidir. Bu süre 60 saniyedir.
- **Göktaşı:** Poligon formatındandır. Üçgen, altıgen, sekizgen, onaltıgen gibi vektör şekillerinden oluşur. Ekranın sağından rastgele anlarda, rastgele boyutlarda, rastgele sayıda giriş yaparak sağdan sola doğru farklı hızlarda hareket ederler. Sahada aynı anda en fazla üç göktaşı olabilir.
- **Kardelik:** Nadir zamanlarda sahnenin herhangibir noktasında farklı çaplarda kara delik dairesi belirir. Mekik kara deliğe belli bir mesafe yaklaştığında kara deliğin çekim gücüne kapılır ve içine doğru çekilir. Yön tuşları ile kontrol zorlaşır. Eğer içine düşerse gemi %50 oranında hasar alır ama yoluna devam edebilir. Yoluna devam ederken hızı belli bir süre boyunca düşük ve sabit kalır, yakıt tüketimi %10 artar.
- **Onarım Gemisi:** Dikdörtgen şeklinde içinde `Repair` yazan aktördür. Ekranın sağından sahneye girer. Uzay gemisinin üstünden geçmesi halinde gemideki hasarda %5, %10, %20 gibi oranlarda iyileştirme sağlar. Ayrıca her onarım gemisi 10, 20, 50 gibi farklı bloklarda yakıt hücresi taşır. Dolayısıyla geminin yakıt hücreleri de bu oranlarda şarj olur. Rastgele zamanlarda sahneye girebilir. Sahnede sadece 1 onarım gemisi bulunabilir.
- **Gezegenler:** Arka planda arada sırada geçen, Z düzleminde en arkada duran dairesel vektörlerdir. Farklı boyutlarda olabilirler. Herhangibir çarpışma kontrolüne dahil olmazlar. Sahne de aynı anda en fazla iki gezegen olabilir. Gezegenlerin adları ve mineral bilgileri sahnede oldukları sürece alt barda görünür.
- **Kuyruklu Yıldızlar:** Nadiren çok yüksek süratte ve eğik çizgi halinde sahneye bir kuyruklu yıldız girer. Kuyruklu yıldızın başı küçük çaplı dairesel bir vektördür. Kuyruk kısmı ise 10 ila 20 derece arasında değişen açılarda uç uca bağlanmış çizgilerden oluşur. Kuyruklu yıldız sahneye ekranın sağ üst köşesi ile ortası arasındaki rastgele bir noktadan giriş yapar ve ekranın alt kısmında çıkana kadar uygun bir eğri üzerinde hareket ederek ilerler. Mekiğe çarpması halinde %100 hasar verir ve oyun sonlanır.
- **Takas Üssü:** Dikdörtgen vektör grafik şekilde içerisinde `Takas Üssü` yazan aktördür. Oyuncu mekiği bu dikdörtgen üssüne getirdiğinde oyun durur ve ticaret yapılabilecek bir ekran açılır. Takas Üssü'nde rastgele minerallerden rastgele yüzdelerde farklı Spaceron tutarlarında stok bulunur. Oyuncu sahip olduğu mineralleri kullanarak takas yapabilir ya da sahip olduğu minerallerden satış yaparak bütçesini artırabilir.

## Görevler

Oyuncu, gezegenlerdeki mineralleri kullanarak çeşitli görevleri tamamlamaya çalışır. Örnek görevler;

- Merkez koloni su arıtma sistemi için temel madde tedariki. 350 Doka Oksijen.
- Orion gemi onarım tesisi için yapı malzemesi. 400 Doka Çelik, 50 Doka İridyum, 25 Doka Tungsten.
- Ay üssü alfa savunma sistemi ana madde tedariği. 500 Doka Kayber Kristal.
- etc

## Grafik

Oyundaki tüm aktörler vektör çizgilerden oluşur. Ekstra asset kullanılmaz.

## İsimlendirmeler

Örnek gezegen adları.

- Grenwood
- Alpha 356
- Odsseyy Hole
- Shock Hole
- Black Widow
- Redroom
- Blinky Shadow
- Tatuyin
- S-Bull
- Hardal
- Edge of the Universe
- etc

Bu örneklere benzer toplamda 20 gezegen adı kullanılır.

## Mineraller

Gezegenler ve hedef olarak gidilen gezegendeki bulunması muhtemel mineraller aşağıdaki gibidir.

- Çelik
- Oksijen
- Tungsten
- Gümüş
- Mavi Kristal
- Kayber Kristal
- İridyum
- Demir
- Silisyum
- U-235
- etc

Gezegenler bu minerallerden aynı anda sadece 3 adedini barındırabilir. Mineral miktarları `Doka` türündendir. Örneğin, ``Hardal` gezegeninde 25 Doka `İridyum` (1 birimi 1000 Spaceron), 25 Doka `Gümüş` (1 birimi 100 Spaceron) ve 50 Doka (%1 birimi 300 Spaceron) kadar `Demir` vardır. `Redroom` gezegeninde ise sadece `Kayber Kristal` vardır ancak 50 Doka kadardır (%1 birimi 5000 Spaceron).

## Birimler

- **Doka:** 1 Doka 1000 birim mineral içerir.
- **Spaceron:** Para birimidir. Gezegenler sahip oldukları minerallar için birim başına kendi Spaceron değerlerini uygularlar. Spaceron değerleri akla yatkındır. Çok uzak gezegenlerde daha düşük ücretlerde olabilir.
- **Celluron:** Mekiğin enerji seviye birimidir. Mekik oyun başlangıcında 1000 `Celluron` enerjiye sahiptir. Yol kat edildikçe 5 saniye 1 birim düşüş yaşanır. Göktaşı çarpışmalarında 100 birim anında düşer.

## Teknoloji

Oyun kodları Python ve ilgili kütüphaneleri kullanılarak geliştirilir.

## Ekran

Oyunun ekran boyutları 900X500 pikseldir.
