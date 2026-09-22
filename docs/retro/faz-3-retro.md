# Faz 3 Retrospektifi — Performans ve Ölçek Baskısı (Hafta 9–12)

## Ne yapıldı

Yirmi gün, dört konu: EF Core performansı (41–45), önbellekleme (46–50),
telemetri hattı ve arka plan servisleri (51–55), eşzamanlılık ve bellek
(56–60).

Yeni projeler: `Scootly.Worker` (üç arka plan servisi) ve
`Scootly.DeviceSimulator` (200 sanal scooter). Yeni alan alanları: `Telemetry`
ve `Pricing`.

## İyi giden

**Bir hatayı iki dalı karşılaştırırken bulmak.** `Ride.StartedAt` hiçbir
sütuna eşlenmemişti ve bunu ancak ekip arkadaşının dalıyla dosya dosya
karşılaştırırken gördük — o, aynı hatayı 24. günde kendi başına bulmuş ve
teknik borç listesine ders olarak yazmıştı. İki kişinin aynı sistemi ayrı ayrı
yazmasının değeri tam olarak bu: aynı tuzağa ikisi birden düşmüyor.

**Hatayı düzeltmek yerine hata SINIFINI kapatmak.** `StartedAt`'e assert
yazmak yeterli olurdu — ama bir sonraki salt okunur özelliği yine kimse fark
etmezdi. `EslemeButunluguTests` modeli yansımayla gezip eşlenmemiş her alanı
yakalıyor. Fark şu: birincisi bilinen bir hatayı, ikincisi bilinmeyen bir
hatayı buluyor.

**Varsayılanı güvenli tarafa çevirmek.** Takipsiz sorgu artık varsayılan;
yazma yolları açıkça `AsTracking()` diyor (ADR 0016). Her okuma sorgusuna
`AsNoTracking()` eklemeyi hatırlamak yerine, unutmanın cezasını gürültülü hale
getirdik: `AsTracking()` demeyi unutan bir yazma yolu sıfır satır yazar ve bu
ilk testte kırmızı olur.

**Application katmanının paket listesi hâlâ boş.** `ToListAsync` EF Core'a ait
olduğu için sorgu handler'larını Application'a taşımak, paketin de oraya
girmesi anlamına gelirdi. `IQueryExecutor` (ADR 0013) bunu engelledi.
38. günde `DbUpdateConcurrencyException` için aynı sınır korunmuştu; o kararın
tutarlı olması bu fazda işe yaradı.

## İyi gitmeyen

**Ölçmeden yazdık.** Bu fazın dürüst olması gereken maddesi bu. Faz 3'ün
neredeyse her günü "ölç ve kaydet" ile bitiyor;
`docs/architecture/system-design.md` içindeki Faz 3 bölümünde şu an **17 adet
`(doldur)`** var. Kod yazıldı, testler yazıldı, ölçüm yapılmadı.

Bu, Faz 2 retrosunda yazdığımız hatanın tekrarı — orada "yirmi gün veritabanı
olmadan yazıldı" demiştik. Aradaki fark: orada ortam eksikti, burada ortam
vardı ve sırayı biz seçtik.

Somut sonucu şu: "AsNoTracking daha hızlı" cümlesi bugün bir **iddia**,
ölçüm değil. Faz 3'ün asıl teslim ettiği şey optimizasyon değil, optimizasyonun
işe yaradığının kanıtıydı. O kanıt henüz yok.

**33.–34. günün ölçümleri de hâlâ boş.** Faz 2'den taşınan borç, Faz 3'te
kapanmadı ve üstüne yenisi eklendi.

**Redis kurulumu ertelendi.** 47. gün iki API kopyasının aynı Redis'i
paylaştığını doğrulamayı istiyordu. Kod yazıldı, `docker-compose.yml`'a servis
eklendi, ama iki kopya birlikte çalıştırılıp doğrulanmadı — yani dağıtık
önbelleğin asıl iddiası (paylaşım) sınanmadı.

**Kırıcı bir API değişikliği yaptık.** `GET /api/v1/vehicles` artık
enlem/boylam istiyor. Gerekçesi savunulabilir (ucun istemcisi yok) ama 30.
günde kurulan sürümleme disiplininin ilk istisnası bu, ve istisnalar böyle
başlıyor.

## Öğrenilen

**Bir hatanın sessiz mi gürültülü mü olduğu, hatanın kendisinden önemli.**
Bu fazda üç kez aynı şeye karar verdik: takip varsayılanı (ADR 0016), kanal
dolduğunda atılan kayıtların sayılması (ADR 0019), `ITelemetryWriter`'ın tek
kayıt almaması. Üçünde de asıl soru "hangisi daha hızlı" değil, "yanlış
yapıldığında nasıl fark edilir" oldu.

**Paralellik varsayılan olarak hızlandırmıyor.** 58. günün tablosu bunu
gösteriyor: küçük gruplarda `Parallel.ForEach` sıralıdan yavaş. Telemetri
hattında paralellik kullanmama kararı bu ölçüme dayanıyor — darboğaz CPU
değil, veritabanına yazmaydı.

**Dağıtık kilit doğruluk garantisi değil.** 50. günün en önemli çıktısı, o
kilidi rezervasyon yolunda KULLANMAMA kararı oldu (ADR 0018). Doğruluk 38.
günün sürüm damgasında kalıyor; kilit yalnızca gereksiz işi azaltmak için.

## Tahmin ve gerçekleşen

| | Tahmin | Gerçekleşen |
|---|---|---|
| Kod yazımı | 20 gün | Tek oturumda yazıldı |
| Ölçüm | Her günün içinde | **Yapılmadı** |
| Redis doğrulaması | 47. gün | Yapılmadı |

Kod yazımının hızlı gitmesi aldatıcı: hızlanan şey yazmaktı, ve bu fazın işi
yazmak değildi.

## Faz 4'e girerken yapılması gereken

1. `dotnet test tests/Scootly.DbLab` ile bütün ölçümleri çalıştırıp
   `system-design.md`'deki 17 `(doldur)`'u doldurmak. Faz 4 (dağıtık ve
   asenkron) bu sayıların üstüne inşa ediliyor; boş bırakılırsa Faz 4'ün
   ölçümlerinin karşılaştıracağı bir taban olmaz.
2. Redis'i Mac tarafında ayağa kaldırıp iki API kopyasıyla paylaşımı
   doğrulamak.
3. Simülatörü 200 araçla çalıştırıp telemetri hattını gerçek yük altında
   görmek — 51–55. günlerin tamamı bu yükü varsayıyor.
