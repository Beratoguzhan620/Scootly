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