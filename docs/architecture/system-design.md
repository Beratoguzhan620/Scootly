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

---

# FAZ 3 — Performans ve ölçek baskısı (Hafta 9–12)

> **Bu bölümdeki tabloların rakamları boş.** Kasıtlı: her satırı üreten test
> adı yazılı ve rakam o testin çıktısından doldurulacak. Başkasının makinesinde
> çıkan bir sayıyı buraya yazmak, ölçmemiş olmanın süslü hali olurdu.
>
> Doldurma komutu her bölümün başında.

## Takip maliyeti (41. gün)

```
dotnet test tests/Scootly.DbLab --filter Gun41
```

1.000 sorgu, her biri 100 satır:

| Sorgu | Süre (ms) | Tahsis (MB) |
|---|---|---|
| `AsNoTracking` | _(doldur)_ | _(doldur)_ |
| `AsTracking` | _(doldur)_ | _(doldur)_ |

Beklenen yön: takipli sorgu **belirgin biçimde daha çok bellek** tahsis eder.
Süre farkı küçük bir makinede gürültüye karışabilir; asıl gösterge tahsis.

Karar ve gerekçesi ADR 0016'da: takipsizlik artık `ScootlyDbContext`'in
varsayılanı, yazma yolları açıkça `AsTracking()` diyor.

## N+1 avı (42. gün)

```
dotnet test tests/Scootly.DbLab --filter Gun42
```

Harita ucunun çalıştırdığı SQL sorgusu sayısı: **tam olarak 2** (biri `COUNT`,
diğeri sayfanın kendisi). Test bunu bir iddiaya bağladı, göz kararı saymıyor.

Toplam kök (aggregate) yüklenseydi sahip olunan tipler (`Model`, `Battery`,
`Location`) için ek sorgular görünür ve sayı satır sayısıyla birlikte
büyürdü — N+1'in bu projedeki hali.

**Geliştirmede SQL'i görmek için:** `appsettings.Development.json` içinde
`Microsoft.EntityFrameworkCore.Database.Command` seviyesi `Information`.
Üretimde kapalı: her sorguyu loglamak hem disk hem performans maliyeti,
ayrıca sorgu parametreleri arasında kişisel veri geçebilir (ADR 0009).

## Yakınlık sorgusu (43. gün)

```
dotnet test tests/Scootly.DbLab --filter Gun43
```

500 m yarıçap, 100 bin araçlık veri kümesi:

| Yöntem | Süre (ms) | Okunan satır | Sonuç |
|---|---|---|---|
| Veritabanı filtresi (sınır kutusu) | _(doldur)_ | _(doldur)_ | _(doldur)_ |
| İstemci tarafı filtre (Haversine, bellekte) | _(doldur)_ | _(doldur)_ | _(doldur)_ |

Asıl fark **okunan satır** sütununda: istemci tarafı filtre bütün tabloyu
ağdan geçirip bellekte eliyor. Bu, veri azken hiç fark edilmez ve veri
büyüdükçe doğrusal olarak kötüleşir.

İki yöntemin **aynı sonucu bulmaması beklenen**: sınır kutusu bir dikdörtgen,
Haversine bir daire. Gerekçe ve kabul edilen hata ADR 0015'te.

## Toplu yazma (44. gün)

```
dotnet test tests/Scootly.DbLab --filter Gun44
```

5.000 telemetri kaydı:

| Yöntem | Süre (ms) |
|---|---|
| `COPY` (`TelemetryBulkWriter`) | _(doldur)_ |
| EF `AddRange` + `SaveChanges` | _(doldur)_ |

### Bağlantı havuzu

Npgsql varsayılan olarak bağlantı havuzu kullanıyor (`Pooling=true`,
`MaxPoolSize=100`). Bağlantı dizesinde değiştirilmedi — ölçülmemiş bir sayıyı
değiştirmek, iyileştirme değil tahmin olurdu.

**`AddDbContextPool` kullanılmadı.** Bağlam havuzlamak, bağlam nesnesinin
kendisini yeniden kullanmak demek ve bu, bağlam üzerinde durum tutan her
şeyin (bizde `OnConfiguring`'deki takip ayarı dahil) havuzlamaya uygun
olmasını gerektiriyor. Kazanç ölçülmedi, risk gerçek: şimdilik hayır.

## Önbellek (46.–50. gün)

```
dotnet test tests/Scootly.DbLab --filter Gun48
```

| Durum | Yanıt süresi (ms) |
|---|---|
| Iska (önbellekte yok, veritabanına gidiliyor) | _(doldur)_ |
| İsabet (Redis'ten) | _(doldur)_ |

Yaşam süreleri ve geçersizleştirme tablosu ADR 0017'de.

## Paralel işleme (58. gün)

```
dotnet test tests/Scootly.DbLab --filter Gun58
```

10.000 kayıt üzerinde mesafe hesabı:

| Yöntem | Süre (ms) | Sıralıya göre |
|---|---|---|
| Sıralı (`foreach`) | _(doldur)_ | 1.00x |
| Paralel, 1'er kayıt | _(doldur)_ | _(doldur)_ |
| Paralel, 10'ar kayıt | _(doldur)_ | _(doldur)_ |
| Paralel, 100'er kayıt | _(doldur)_ | _(doldur)_ |
| Paralel, 1000'er kayıt | _(doldur)_ | _(doldur)_ |

Beklenen: küçük gruplarda paralellik **kaybediyor**. Her grup için ödenen ek
yük (görev oluşturma, iş parçacığına dağıtma, bağlam değiştirme) grubun kendi
işinden büyük.

Bu tablodan çıkan karar: telemetri işleme hattında `Parallel.ForEach`
kullanılmıyor. Gerçek darboğaz CPU değil, veritabanına yazma — ve orada çözüm
paralellik değil toplu yazma.

## Bellek (59. gün)

```
dotnet test tests/Scootly.DbLab --filter Gun59
```

10.000 kayıt için metin birleştirme:

| Yöntem | Tahsis (KB) |
|---|---|
| Döngü içinde `string +=` | _(doldur)_ |
| `StringBuilder` (ön boyutlu) | _(doldur)_ |

`string` değişmez olduğu için döngü içinde `+=` her adımda eski metnin
tamamını yeni bir diziye kopyalıyor: N adımda tahsis N'in karesiyle büyüyor.
On kayıtla fark yok, on bin kayıtla yüzlerce kat.

### Konteyner bellek limiti

`deploy/docker-compose.yml` içinde Redis servisine 384 MB limit konuldu ve
Redis'in kendi `maxmemory` ayarı 256 MB. İkisi arasındaki fark bilinçli:
Redis'in kendi ek yükü için pay bırakıyor, yoksa Redis kendi sınırına varmadan
konteyner öldürülürdü (OOM kill).

.NET tarafında konteyner limiti çöp toplayıcının davranışını değiştiriyor:
çalışma zamanı cgroup limitini okuyup yığın bütçesini ona göre ayarlıyor.
Limit konmasaydı GC makinenin tüm belleğini kendi bütçesi sanardı.

## Süreç, iş parçacığı, async (56.–57. gün)

```
dotnet test tests/Scootly.DbLab --filter Gun56
dotnet test tests/Scootly.DbLab --filter Gun57
```

| Deney | Beklenen sonuç |
|---|---|
| 256 eşzamanlı iş, `await` ile | Hepsi ~işin kendi süresi kadar sürede biter |
| 256 eşzamanlı iş, `.Result` ile | Belirgin biçimde yavaş — iş parçacığı havuzu açlığı |
| 8 iş parçacığı × 100.000 `sayac++` | Beklenenden **az** (kayıp artışlar) |
| Aynısı `Interlocked.Increment` ile | Tam olarak beklenen |

Üretimdeki görünümü: yük arttıkça yanıt süreleri sıçrıyor ama **CPU düşük**.
Kimse çalışmıyor, herkes bekliyor.
