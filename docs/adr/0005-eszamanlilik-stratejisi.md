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

## Deadlock Gözlemi (37. gün)

İki araç satırını ters sırayla kilitleyen iki eşzamanlı transaction, PostgreSQL
tarafından deadlock olarak tespit edildi ve taraflardan biri otomatik iptal edildi
(hata kodu 40P01). Aynı iki transaction'ı **aynı sırayla** kilitleyecek şekilde
değiştirince deadlock hiç oluşmadı — ikinci transaction sadece ilkinin bitmesini
sırayla bekledi.

**Kural:** Projede birden fazla kaynağa (birden fazla Vehicle satırı, ileride
Vehicle+Wallet gibi farklı tablolar) aynı işlemde erişen her kod yolu, bu kaynaklara
tutarlı bir sırayla (örnek: her zaman Id'ye göre küçükten büyüğe) erişmelidir.
Bu proje şu an çoklu kaynak kilitleyen bir üretim kodu içermiyor (DeadlockTests
yalnızca bu deneyi kanıtlamak için yazıldı), ama 14. haftada (Saga desenleri)
bu kural devreye girecek.

## Çözüm Uygulandı (38. gün)

İlk denemede `byte[] RowVersion` + `IsRowVersion()` deseni kullanıldı — bu SQL
Server'a özgü bir yaklaşımdır ve PostgreSQL'de hiçbir otomatik değer üretmediği
için koruma gerçekte çalışmadı (Başarılı: 40/100, baseline'dan bile kötü).

Doğru çözüm: PostgreSQL'in her satırda doğal olarak bulunan `xmin` sistem sütunu,
EF Core'a bir gölge alan (`builder.Property<uint>("xmin").IsRowVersion()`) olarak
tanıtıldı. Bu, veritabanı şemasında (mantıksal olarak) yeni bir sütun gerektirmez
— PostgreSQL'in kendi iç mekanizmasını kullanır. Domain sınıfı (Vehicle) hiçbir
eşzamanlılık detayı bilmez, tamamen Infrastructure katmanında kalır.

**Doğrulama sonucu (baseline ile karşılaştırma):**

| İstek Sayısı | Öncesi (36. gün) | Sonrası (38. gün) |
|---|---|---|
| 10 | 4 başarılı | **1 başarılı**, 9 Conflict |
| 50 | 9 başarılı | **1 başarılı**, 49 Conflict |
| 100 | 30 başarılı | **1 başarılı**, 99 Conflict |

`ReserveVehicleCommandHandler`, `DbUpdateConcurrencyException`'ı yakalayıp
kullanıcıya "başka biri tarafından rezerve edildi, tekrar deneyin" mesajıyla
409 Conflict döndürüyor — çırılçıplak bir 500 hatası değil.

## Ders (Platform Farkı)
SQL Server'a özgü desenler (`rowversion`/`timestamp` tipi, `[Timestamp]` attribute'ü)
PostgreSQL'de doğrudan çalışmaz. Veritabanı motoruna özgü eşzamanlılık mekanizmalarını
(PostgreSQL için `xmin`) araştırıp kullanmak gerekir.