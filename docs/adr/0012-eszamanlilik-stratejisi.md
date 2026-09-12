# 0012 — Eşzamanlılık stratejisi

- **Durum:** Kabul edildi
- **Tarih:** 36.–40. gün
- **Bağlam:** Projenin en kritik kuralı — bir araç aynı anda yalnızca bir
  kişiye rezerve edilebilir — 36. güne kadar hiçbir şekilde korunmuyordu.

## Önce görüldü, sonra çözüldü

`Gun36_Korumasiz_kodda_ayni_arac_birden_fazla_kisiye_rezerve_edilir` testi,
aynı araca 50 eşzamanlı rezervasyon isteği gönderiyor ve **birden fazlasının
başarılı olduğunu** gösteriyor. Her istek "araç müsait mi" diye soruyor, hepsi
"evet" cevabını alıyor (çünkü hiçbiri diğerinin henüz yazmadığı veriyi
okuyor) ve hepsi rezervasyonu tamamlıyor.

Bu testi önce yazmanın sebebi: çözümün gerçekten çalıştığını başka türlü
bilemezdik. Tek iş parçacıklı bir testle bu hata asla görülmez.

## Karar 1 — Ana strateji iyimser eşzamanlılık (sürüm damgası)

`Vehicles` tablosuna bir `Version` sütunu eklendi. Bir işlem satırı okuduğunda
mevcut sürümü de okur; güncellerken `WHERE ... AND "Version" = okudugum` der.
Araya başka bir işlem girdiyse sürüm değişmiştir, güncelleme sıfır satır etkiler
ve çakışma tam o anda tespit edilir.

**Neden iyimser:** Çakışma bu sistemde **nadir**. İki kişinin aynı saniyede aynı
scooter'ı seçmesi istisna, kural değil. İyimser yaklaşım kilit tutmaz, sadece
yazma anında kontrol eder — yani normal durumda hiçbir bedeli yoktur. Kötümser
kilitleme her okumada kilit tutar; nadir bir çakışma için her isteği
yavaşlatmak olurdu.

## Karar 2 — Sürüm damgası gölge (shadow) özellik

`Vehicle` alan sınıfında `Version` diye bir alan **yok**. Damga yalnızca EF
modelinde var.

**Neden:** `Vehicle` bir iş kavramı; "bu satır kaç kez güncellendi" ise bir
kalıcılık detayı. Alan modeline eklemek, aracın iş kurallarıyla hiç ilgisi
olmayan bir sayıyı domain'e sokmak olurdu.

Değeri `ScootlyDbContext.SaveChangesAsync` artırıyor — EF damgayı kendiliğinden
artırmaz, yalnızca okuduğu değeri `WHERE` koşuluna koyar. Artırmayı tek bir
yerde yapmak, her aggregate için ayrı yazılmasını ve birinin unutulup sessizce
korumasız kalmasını önlüyor.

## Karar 3 — `IsRowVersion()` KULLANILMADI

**Bu bir tuzak ve yazıya geçmesi gerekiyor.**

`IsRowVersion()`, SQL Server'ın `rowversion` tipine karşılık gelir.
PostgreSQL'de böyle bir tip yoktur. Npgsql ile kullanıldığında beklenen
korumayı sağlamaz — ve başarısızlığı **sessizdir**: derleme geçer, testler
yeşil görünür, ama iki kişi aynı aracı kiralayabilir.

PostgreSQL'e özgü alternatif, sistem sütunu `xmin`'i damga olarak kullanmaktır.
Onu seçmedik çünkü sağlayıcıya özgü ve migration üretimiyle sürtüşüyor
(`xmin` her tabloda zaten var, ama EF onu eklenmesi gereken bir sütun sanıyor).
Açık bir `Version` sütunu her veritabanında aynı şekilde çalışıyor ve modelde
görünür duruyor.

## Karar 4 — İzolasyon seviyesi `ReadCommitted` kalıyor

37. gündeki ölçüm (`Gun37_Izolasyon_seviyeleri_karsilastirilir`) üç seviyeyi
karşılaştırıyor. Sonuç: `Serializable` çift rezervasyonu engelliyor, ama bunu
işlemleri iptal ederek yapıyor — iptal edilen her işlem kullanıcıya bir hata
ya da bir yeniden deneme demek.

Sürüm damgası aynı korumayı, izolasyon seviyesini yükseltmeden ve **yalnızca
gerçekten çakışan** işlemleri reddederek sağlıyor. Bu yüzden seviye
`ReadCommitted` (PostgreSQL varsayılanı) kalıyor.

"En güvenlisini seçelim" deyip `Serializable`'a geçmek, bedelini ölçmeden
alınmış bir karar olurdu. Tablo o ölçüm.

## Karar 5 — Kötümser kilitleme nerede kullanılır

`VehicleRepository.GetByIdForUpdateAsync` yazıldı ama üretim yolunda
kullanılmıyor. Kullanılacağı durum: çakışmanın **sık** olduğu ve yeniden
denemenin pahalı olduğu bir işlem — örneğin ileride gelecek toplu ödeme
mutabakatı. Orada iyimser yaklaşım sürekli başarısız deneme üretirdi.

Arayüze (`IVehicleRepository`) bilerek eklenmedi: kilitleme bir kalıcılık
stratejisi, arayüze konsaydı her uygulayan ve her test sahtesi satır kilidi
kavramını bilmek zorunda kalırdı.

## Karar 6 — Kilit sırası disiplini

Birden fazla tablo kilitlenecekse **her yerde aynı sırayla**: önce `Vehicles`,
sonra `Rides`.

39. gündeki test iki işlemi ters sırada kilitletiyor ve PostgreSQL'in deadlock'u
kendisi tespit edip (SQLSTATE `40P01`) taraflardan birini iptal ettiğini
gösteriyor. Aynı sırayla kilitlendiğinde deadlock hiç oluşmuyor — ikinci işlem
birincinin bitmesini bekleyip sırayla tamamlanıyor.

Deadlock ile kilit bekleme farkı burada görünür hale geliyor: bekleme normaldir
ve biter; deadlock bitmez, veritabanı müdahale etmek zorunda kalır.

## Karar 7 — Çakışma çözümü: kullanıcıya söyle, otomatik deneme yapma

Çakışma tespit edildiğinde kullanıcı 409 ve "Biri sizden önce davrandı" mesajı
alıyor.

**Neden otomatik yeniden deneme yok:** Araç kiralama gibi bir işlemde sessizce
tekrar denemek, kullanıcının artık istemediği ya da yanındaki başka bir aracı
kiralamasına yol açabilir. Kullanıcı hangi aracı istediğini biliyor; kararı ona
bırakmak doğru.

## Karar 8 — EF'in istisnası Application katmanına sızmıyor

`DbUpdateConcurrencyException` Infrastructure'da yakalanıp
`ConcurrencyConflictException` (Application katmanında tanımlı) olarak yeniden
fırlatılıyor.

**Neden:** Handler'lar EF'in istisnasını doğrudan yakalasaydı Application
projesinin EF Core paketine bağımlı olması gerekirdi — ve o bağımlılık bir kez
girdiğinde "madem EF zaten var" diyerek başka EF tiplerinin de sızmasının önü
açılırdı. Karar 1'in (Application somut teknolojiden bağımsız olmalı) korunması.
