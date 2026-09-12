# 0011 — Şema, indeksleme ve işlem sınırı

- **Durum:** Kabul edildi
- **Tarih:** 31.–35. gün

## Karar 1 — Ölçüm projesi Testcontainers kullanmıyor

`tests/Scootly.DbLab`, çalışan PostgreSQL'e bağlanıp kendi ayrı veritabanını
(`scootly_lab`) oluşturuyor.

**Neden:** Geliştirme makinesi Apple Silicon üzerinde Parallels ile çalışan bir
Windows sanal makinesi; Docker macOS tarafında ve misafirden erişilemiyor.
Docker Desktop'ın "Expose daemon on tcp://" seçeneği bunu çözerdi ama daemon'ı
**kimlik doğrulamasız** açar — o porta ulaşan herkes ana makinede root
yetkisine sahip olur. Hermetik test iyi bir şey ama bu bedele değmez.

**Bedeli kayıtlı:** testler hermetik değil, aynı sunucuyu paylaşıyorlar. CI'da
(ileride) Testcontainers'a dönülecek; orada Docker zaten yerel.

Veritabanı yoksa testler **kırmızı** oluyor, atlanmıyor. Sessiz atlama, bozuk
bir ortamın fark edilmeden geçmesine yol açar ve bir süre sonra "o testler
zaten hep atlanıyor" haline gelir.

## Karar 2 — Kısmi (partial) indeks

`ix_vehicles_durum`, `Status` sütununda ama `WHERE "Status" = 'Available'`
koşuluyla.

**Neden:** `Status` yalnızca dört değer alıyor — seçiciliği düşük. Düşük
seçicilikli bir sütunu tam indekslemek genellikle işe yaramaz; planlayıcı
indeksi görmezden gelip sıralı tarama yapar, çünkü satırların dörtte birine
gitmek için indeks okumak tabloyu taramaktan pahalıdır. Kısmi indeks yalnızca
ilgilenilen satırları tuttuğu için hem küçük hem isabetli.

## Karar 3 — Sayısal tipler küçültülmedi

`BatteryPercentage` ve `RangeKm` `integer` kaldı.

**Neden:** `smallint` 2 bayt tasarrufu sağlardı — 100 bin satırda 200 KB, bir
milyon satırda 2 MB. Buna karşılık `int` özelliğini `smallint` sütuna eşlemek
okuma tarafında tip dönüşümü riski getiriyor. Ölçülen kazanç alınan riski
karşılamıyor.

**Bu bir "yapmadık" değil, "ölçtük ve yapmamaya karar verdik".** Tablo on
milyon satıra çıkarsa yeniden bakılacak. Kaydın kendisi, ileride birinin aynı
soruyu sıfırdan araştırmasını önlüyor.

## Karar 4 — `Fare` için `numeric(10,2)`, `double` değil

Para hesabında kayan nokta kullanmak `0.1 + 0.2 = 0.30000000000000004` sınıfı
hatalara yol açar ve bu hatalar fatura toplamlarında birikir. `numeric` ondalık
aritmetiği tam yapar; bedeli biraz daha yavaş olmasıdır ve bu bedel para için
tartışmasız kabul edilir.

## Karar 5 — İşlem sınırı `ITransactionManager` üzerinden, `IUnitOfWork`'e eklenmedi

**Neden:** İkisi farklı sorumluluk. "Biriken değişiklikleri kaydet" ile "işlem
sınırını yönet" aynı şey değil; çoğu komut handler'ının yalnızca birincisine
ihtiyacı var. İkisini tek arayüzde toplamak, hiç transaction açmayacak olan her
handler'ı ve her test sahtesini o metodu uygulamaya zorlardı — nitekim mevcut
iki test sahtesi tam olarak bu yüzden kırılacaktı.

`SaveChangesAsync` zaten kendi başına atomik; EF Core her çağrıyı örtük bir
transaction içine alır. Açık transaction, bir komutun **birden fazla** kaydetme
yaptığı ya da birden fazla toplam kökü aynı anda tutarlı bırakması gerektiği
durumlar için.

## Karar 6 — İşlem içinde dış servis çağrısı yok

Bir ödeme veya cihaz komutu çağrısı transaction'ın içine girerse, veritabanı
kilidinin süresi **ağ gecikmesine bağlanır**: dış servis üç saniye
yavaşladığında satır üç saniye kilitli kalır ve o araca dokunmak isteyen herkes
bekler. Mevcut handler'lar tarandı; dış servis çağrısı yapan yok. Faz 4'te ödeme
zinciri gelince bu kural yazılı olarak burada duruyor olacak.
