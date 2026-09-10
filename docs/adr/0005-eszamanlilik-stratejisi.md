# ADR 0005: Eşzamanlılık Stratejisi

## Durum
Araştırma tamamlandı, karar 38. günde uygulanacak.

## Bağlam
31. günde, 50 eşzamanlı rezervasyon isteğinden 7'sinin başarılı olduğu (aynı aracın
birden fazla sürücüye rezerve edildiği) bir yarış durumu kanıtlandı. 32. günde üç
farklı transaction izolasyon seviyesi denendi:

| Seviye | Başarılı / Toplam |
|---|---|
| ReadCommitted (varsayılan) | 18 / 20 |
| RepeatableRead | 1 / 20 |
| Serializable | 1 / 20 |

## Karar
İzolasyon seviyesini yükseltmek (RepeatableRead veya Serializable) teknik olarak
çalışıyor ama iki dezavantajı var: (1) çakışan her istek bir veritabanı hatası
fırlatıyor, bunun uygulama katmanında düzgün yakalanıp kullanıcıya anlamlı bir
mesajla (409 Conflict gibi) dönüştürülmesi gerekiyor; (2) yüksek trafik altında
performans bedeli getirebiliyor çünkü çakışan transaction'lar yeniden denenmek
zorunda kalabiliyor.

Bunun yerine 38. günde iyimser eşzamanlılık (optimistic concurrency / sürüm damgası)
uygulanacak: Vehicle tablosuna bir sürüm sütunu eklenip, EF Core'un yerleşik
eşzamanlılık kontrolü kullanılacak. Bu, ReadCommitted seviyesinde kalıp, çakışmayı
uygulama seviyesinde (veritabanı motorunun genel kilit davranışına güvenmeden,
yalnızca ilgili satıra özel) tespit etmeyi sağlıyor.

## Alternatif: Serializable seviyesini varsayılan yapmak
Tüm uygulamanın transaction seviyesini Serializable'a çekmek.

## Neden Seçilmedi
Bu, yalnızca Vehicle.Reserve() gibi çakışmaya açık birkaç işlem için gereken korumayı,
tüm veritabanı işlemlerine (çoğu çakışmaya hiç açık olmayan basit okuma/yazmalar dahil)
dayatmış olurdu — gereksiz performans kaybı.

## Baseline Ölçümü (36. gün)

Koruma eklenmeden önce, farklı yük seviyelerinde ölçüm:

| İstek Sayısı | Başarılı (olması gereken: 1) | Conflict (409) |
|---|---|---|
| 10 | 4 | 6 |
| 50 | 9 | 41 |
| 100 | 30 | 70 |

Gözlem: Yük arttıkça hatalı başarı sayısı da artıyor (%40 → %18 → %30, tutarsız
ama her zaman >1). Bu tablo, 38. günde sürüm damgası eklendikten sonra "Başarılı"
sütununun her seviyede tam olarak 1'e düşmesini doğrulamak için referans olarak
kullanılacak.