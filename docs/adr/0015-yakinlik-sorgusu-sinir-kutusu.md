# ADR 0015 — Yakınlık sorgusu sınır kutusuyla, PostGIS ile değil

- **Durum:** Kabul edildi
- **Gün:** 42–43
- **Bağlam:** Faz 3, Hafta 9 — sorgu iyileştirme

## Karar

"Şu noktanın N metre çevresindeki araçlar" sorgusu, enlem/boylam üzerinde bir
**dikdörtgen sınır kutusu** (bounding box) ile veritabanı tarafında
filtreleniyor. PostGIS kurulmadı.

## Sorun

42. güne kadar bu ucun adı "yakındaki araçlar"dı ama içinde hiçbir konum
filtresi yoktu: tablodaki her aracı sayfalayarak döndürüyordu. Ad ile davranış
arasındaki bu fark, elli araçla çalışıyor görünür, beş bin araçla harita
ekranını kullanılamaz yapar.

Doğru cevap Haversine mesafesi — ama Haversine bir SQL sorgusuna çevrilemez.
Çevrilemeyen bir ifadeyi LINQ'e yazmanın sonucu, filtrenin uygulama belleğinde
çalışması: **istemci tarafı değerlendirme**. Yani veritabanı bütün satırları
ağdan geçirir, uygulama üçünü seçer.

## Karar detayı

Filtre iki adıma bölündü:

1. **Veritabanında, kaba ve çevrilebilir:** dikdörtgen sınır kutusu. İki
   `BETWEEN` koşulu, ve `ix_vehicles_konum` bileşik indeksini kullanabiliyor.
2. **Gerekirse bellekte, hassas:** Haversine — yalnızca dönen küçük küme
   üzerinde. Bugün bu adım uygulanmıyor; dikdörtgen sonucu doğrudan dönüyor.

Boylam deltası enleme göre düzeltiliyor: 60. enlemde bir boylam derecesi
ekvatordakinin yarısı kadar mesafedir. Bu düzeltme yapılmasaydı kuzeyde kutu
gereğinden dar çıkar ve **araç kaybedilirdi** — yani hata, sessizce eksik
sonuç döndürmek olurdu.

## Kabul edilen hata

Dikdörtgen daireden büyük: köşelerde yarıçapın ~1,41 katına kadar uzanıyor.
Yani birkaç fazla araç dönüyor.

Bu bilinçli bir tercih: **fazla kayıt, eksik kayıttan iyi bir hata türü.**
Kullanıcı haritada biraz uzaktaki bir aracı görürse yürümeye karar verir;
görmediği bir aracı kiralayamaz.

## Neden PostGIS değil

PostGIS gerçek çözüm olurdu: `ST_DWithin` ile tam mesafe, GiST indeksiyle
hızlı. Kurulmadı çünkü:

- Geliştirme ortamı Apple Silicon üzerinde Parallels ile çalışan bir Windows
  misafiri; Postgres macOS tarafında duruyor. PostGIS eklentisi kurmak, o
  kurulumu da ekibin diğer iki makinesinde tekrarlamak demek.
- `postgis/postgis` imajına geçmek, 31. günde sabitlenen `postgres:16-alpine`
  sürümünü değiştirmek anlamına geliyor.
- Bugünkü veri boyutunda sınır kutusu yeterli ve ölçüldü.

Bu bir erteleme, reddetme değil. Veri gerçekten büyüdüğünde ya da "en yakın
5 araç" gibi sıralama gerektiren bir ihtiyaç doğduğunda sınır kutusu yetmez.
Teknik borç listesinde.

## Ölçüm

`docs/architecture/system-design.md` → "Yakınlık sorgusu (43. gün)".
Testi: `tests/Scootly.DbLab/Gun42_43_SorguTests.cs`.
