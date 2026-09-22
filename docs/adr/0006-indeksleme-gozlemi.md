# ADR 0006: İndeksleme Gözlemi — Ne Zaman İşe Yaramaz

## Durum
Gözlem kaydedildi.

## Bağlam
33. günde `Vehicles` tablosuna konum (Latitude+Longitude) ve durum (Status) indeksleri
eklendi. 10.000 rastgele üretilmiş kayıt üzerinde "bölge içi müsait araç" sorgusu,
indeks öncesi 224ms, indeks sonrası 252ms sürdü — ölçülebilir bir iyileşme olmadı.

`EXPLAIN ANALYZE` ile gerçek (küçük, tek kayıtlı) veritabanında plan incelendiğinde,
PostgreSQL'in indeksi kullanmayıp "Seq Scan" (sıralı tarama) yaptığı görüldü.

## Gözlem
İki sebep tespit edildi:
1. Test sorgusu, toplam verinin ~%24'ünü (2415/10000) döndürüyor — bu, indeksin
   avantajlı olduğu tipik "seçici" bir sorgu değil (genel kural: %5 altı seçicilik
   indeksten fayda görür, üzeri genelde sıralı taramadan daha yavaş olabilir).
2. Rastgele üretilen test verisinin coğrafi dağılımı, sorgunun filtre aralığıyla
   örtüşecek şekilde tasarlanmadı — gerçekçi bir "şehrin küçük bir bölgesinde arama"
   senaryosunu simüle etmiyor.

## Karar
İndeksler kod tabanında bırakıldı çünkü ileride (gerçek kullanıcı verisiyle, şehrin
tamamına yayılmış binlerce araçtan küçük bir mahalledeki birkaç aracı bulma gibi
gerçekçi ve seçici sorgularda) fayda sağlayacakları öngörülüyor. Ancak "indeks ekledim,
otomatik hızlandı" varsayımı yanlış — her indeks kararı execution plan ile doğrulanmalı.

## Ders
İndeksleme kararları körlemesine değil, gerçek veri dağılımı ve execution plan
analiziyle verilmeli. Bu proje ölçeğinde (gerçek kullanıcı verisi olmadan) anlamlı
bir "önce/sonra" karşılaştırması yapmak zordur — 9. haftada (performans odaklı
haftada) daha gerçekçi veri hacmiyle bu deney tekrarlanabilir.

## Güncelleme (34. gün)

Id bazlı arama testi eklendi ve ölçüm parçalara bölündü:
- Nesne oluşturma döngüsü: ~1372 ms
- SaveChanges (10.000 tekil INSERT): ~2532 ms
- İlk sorgu (soğuk başlama): ~277 ms
- İkinci sorgu (ısınmış, aynı sorgu tekrar): ~6 ms

**Sonuç:** Birincil anahtar indeksi beklendiği gibi çok hızlı çalışıyor (6 ms).
33. gündeki "indeks işe yaramadı" gözlemi, küçük tablo + düşük seçicilikten
kaynaklanıyordu; bu doğrulandı. Ayrıca "soğuk başlama" maliyetinin tek ölçümlü
performans testlerini yanıltabileceği görüldü — en az iki ölçüm alıp ilkini
ısınma turu saymak daha güvenilir.

**Asıl darboğaz** indeksleme değil, toplu INSERT performansı olarak tespit edildi
(SaveChanges: 2532 ms / 10.000 kayıt). Bu, 44. günde (toplu yazma) ele alınacak.

## AsNoTracking Gözlemi (41. gün)

34. günde AsNoTracking'in fark yaratmadığı gözlemlenmişti, ama o ölçüm SaveChangesAsync
(10.000 INSERT) maliyetiyle karışıktı. 41. günde, yalnızca okuma sorgusu izole edilerek
tekrar ölçüldü:

- Tracking (izlenen): 213 ms / 5.000 kayıt
- AsNoTracking: 125 ms / 5.000 kayıt (~%41 iyileşme)

**Sonuç:** AsNoTracking, saf okuma sorgularında gerçek ve ölçülebilir bir fayda
sağlıyor — özellikle çok sayıda kayıt dönen listelerde. Bu yüzden VehiclesController
ve RidesController'daki tüm salt-okunur sorgulara (GetNearby, GetById) AsNoTracking
eklendi. Veriyi değiştiren sorgulara (Register, Reserve gibi) eklenmedi çünkü EF Core'un
değişiklik tespiti için izlemeye ihtiyacı var.