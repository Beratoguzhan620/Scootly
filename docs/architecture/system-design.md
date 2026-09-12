# Sistem Tasarımı — Veritabanı Notları

Bu dosya 31.–35. günlerde doğrudan veritabanı üzerinde yapılan çalışmanın
notlarını tutar.

## Ölçüm ortamı

Ölçümler `tests/Scootly.DbLab` projesinden yapılıyor. Proje Testcontainers
kullanmıyor; çalışan PostgreSQL üzerinde kendi ayrı veritabanını
(`scootly_lab`) oluşturup orada çalışıyor.

**Neden Testcontainers değil:** Geliştirme makinesi Apple Silicon üzerinde
Parallels ile çalışan bir Windows sanal makinesi. Docker macOS tarafında
duruyor ve misafir işletim sisteminden erişilemiyor. Docker Desktop'ın
"Expose daemon on tcp://" seçeneği bunu çözerdi ama daemon'ı kimlik
doğrulamasız açar — o porta ulaşan herkes ana makinede root yetkisine sahip
olur. Bu bedel, hermetik test uğruna ödenmeye değmedi.

**Bedeli:** Testler hermetik değil, aynı sunucuyu paylaşıyorlar ve sunucu
ayakta değilse kırmızı oluyorlar (sessizce atlanmıyorlar — bozuk bir ortamın
fark edilmeden geçmesini istemiyoruz).

Çalıştırma:

```bash
bash lab.sh
```

## Veri kümesi (31. gün)

`tests/Scootly.DbLab/sql/01-ornek-veri.sql` — 100.000 araç ve ~100.000 sürüş.

`generate_series` ile tek komutta üretiliyor; tek tek `INSERT` yerine bu
biçimin seçilmesi 100 bin satırı saniyeler içinde yazmayı sağlıyor.

Betiğin sonundaki `ANALYZE` satırları önemli: PostgreSQL'in planlayıcısı tablo
istatistiklerine bakarak karar verir ve toplu yüklemeden hemen sonra bu
istatistikler eskidir. `ANALYZE` çalıştırılmazsa planlayıcı tablonun hâlâ boş
olduğunu sanıp yanlış plan seçer — ve 34. gündeki "tahmini satır sayısı ile
gerçek satır sayısı" farkı ölçümün kendisinden değil ihmalden gelirdi.

## On doğrulama sorgusu (31. gün)

`tests/Scootly.DbLab/sql/02-dogrulama-sorgulari.sql`

| # | Sorgu | Ne öğretiyor |
|---|---|---|
| 1 | Durumlara göre araç sayısı | `GROUP BY` temeli |
| 2 | Son 24 saatte tamamlanan sürüş | Zaman aralığı filtresi |
| 3 | Yakındaki müsait araçlar (kutu filtresi) | 33. günün ölçülen sorgusu |
| 4 | Marka bazında ortalama batarya | Toplama fonksiyonları |
| 5 | Şarj gerektiren araçlar | Bileşik `WHERE` |
| 6 | INNER JOIN — sürüşü olan araçlar | Yalnızca eşleşen satırlar |
| 7 | LEFT JOIN — hiç kullanılmamış araçlar | Eşleşmeyen satırların da gelmesi |
| 8 | Günlük ciro (son 7 gün) | Tarihe göre gruplama |
| 9 | Alt sorgu — ortalama üstü ücretler | Gömülü sorgu |
| 10 | Aynı sorgunun JOIN hali | Planlayıcının ikisini aynı biçimde optimize edip etmediği |

Sonuçları görmek için:

```bash
dotnet test tests/Scootly.DbLab --filter Gun31 --logger "console;verbosity=detailed"
```

## İndeksler (33. gün)

| İndeks | Tablo | Sütunlar | Gerekçe |
|---|---|---|---|
| `ix_vehicles_konum` | Vehicles | (Latitude, Longitude) | "Yakındaki araçlar" kutu filtresi |
| `ix_vehicles_durum` | Vehicles | Status **WHERE Status='Available'** | Kısmi indeks |
| `ix_rides_surucu` | Rides | DriverId | Sahiplik kontrolü, sürüş geçmişi |
| `ix_rides_arac` | Rides | VehicleId | JOIN'ler |
| `ix_rides_durum_bitis` | Rides | (Status, EndedAt) | "Son 24 saatte tamamlanan" |

İki karar not edilmeye değer:

**Kısmi indeks.** `Status` yalnızca dört değer alıyor — seçiciliği düşük, tek
başına indekslemek pek işe yaramaz. Ama sorguların çoğu yalnızca müsait
araçlarla ilgileniyor; `WHERE Status = 'Available'` koşullu indeks yalnızca o
satırları tutuyor, yani hem küçük hem isabetli.

**Bileşik indekste sıra.** `(Status, EndedAt)` — eşitlik koşulu önce, aralık
koşulu sonra. Sıra ters olsaydı indeks aralıktan sonrasını kullanamazdı.
Aynı sebeple `(Latitude, Longitude)` ikisi de aralık olduğu için sıraya daha
az duyarlı.

Mesafe hesabının (Haversine) kendisi indeksten faydalanamaz, çünkü sütunlar bir
fonksiyonun içine girer ve B-ağacı indeksi o hali göremez. "Önce kaba kutu,
sonra hassas mesafe" deseninin sebebi bu.

## Ölçüm sonuçları (33.–34. gün)

> Aşağıdaki tablo `dotnet test tests/Scootly.DbLab --filter Gun33`
> çıktısından doldurulacak. Rakamlar makineye ve veri dağılımına bağlı;
> başkasının sayısını yazmanın anlamı yok.

| Durum | Medyan süre | Plan |
|---|---|---|
| İndeks yok | _(doldur)_ | _(Seq Scan bekleniyor)_ |
| İndeks var | _(doldur)_ | _(Index / Bitmap Scan bekleniyor)_ |

### İndeks öncesi execution plan

```
(test çıktısından yapıştır)
```

### İndeks sonrası execution plan

```
(test çıktısından yapıştır)
```

Planlarda bakılacaklar:

- `Seq Scan` mı `Index Scan` / `Bitmap Index Scan` mı?
- `rows=` (tahmin) ile `actual rows=` (gerçek) arasındaki fark ne kadar?
  Büyük fark, istatistiklerin eski olduğunu gösterir.

## Şema gözden geçirmesi (32. gün)

| Alan | Önce | Sonra | Karar |
|---|---|---|---|
| `Vehicles.Brand` | varchar(100) | varchar(64) | En uzun gerçek marka adı 21 karakter; 64 rahat bir tavan |
| `Vehicles.BatteryPercentage` | integer | integer | **Değiştirilmedi** — aşağıya bak |
| `Vehicles.RangeKm` | integer | integer | **Değiştirilmedi** |
| `Rides.Fare` | numeric | numeric(10,2) | Kuruş hassasiyeti açıkça sabitlendi |

**Sayısal tipler neden değiştirilmedi:** `BatteryPercentage` 0–100 arası, yani
`smallint` (2 bayt) yeterli olurdu; `integer` 4 bayt. 100 bin satırda fark
200 KB, bir milyon satırda 2 MB. Buna karşılık `int` özelliğini `smallint`
sütuna eşlemek okuma tarafında tip dönüşümü riski getiriyor. Ölçülen kazanç
alınan riski karşılamıyor; karar "şimdilik değiştirme" ve gerekçesi bu satır.
Tablo on milyon satıra çıkarsa yeniden bakılacak.

**`Fare` neden `numeric`, `double` değil:** Para hesabında kayan nokta
kullanmak `0.1 + 0.2 = 0.30000000000000004` sınıfı hatalara yol açar ve bu
hatalar fatura toplamlarında birikir.

## Transaction sınırı (35. gün)

`TransactionBehavior`, bir komutun çalışmasını tek bir işlem sınırına alıyor.

**Sınır use case ile örtüşüyor** — ne daha geniş, ne daha dar. Controller'da
başlatılsaydı sınır HTTP isteğiyle örtüşürdü ve aynı istekte birden fazla iş
yapıldığında hepsi tek kilit altında kalırdı. Repository'de başlatılsaydı her
okuma/yazma ayrı bir işlem olur, aralarında tutarsız bir an doğardı.

**İşlem içinde dış servis çağrılmıyor.** Bir ödeme veya cihaz komutu çağrısı
transaction'ın içine girerse veritabanı kilidinin süresi ağ gecikmesine
bağlanır: dış servis üç saniye yavaşladığında satır üç saniye kilitli kalır ve
o araca dokunmak isteyen herkes bekler. Mevcut handler'lar tarandı; dış servis
çağrısı yapan yok.
