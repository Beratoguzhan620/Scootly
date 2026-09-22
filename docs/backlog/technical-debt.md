# Teknik Borç Listesi

Bu dosya, bilinçli olarak şimdi düzeltilmeyen ama fark edilen eksiklikleri kaydeder.

## Faz 1 sonu itibariyle

- `CancelReservationCommand` yazıldı ama handler'ı yok (7. gün — dokümanda bilinçli olarak bırakılmıştı, ileride tamamlanacak).
- `CompleteRideCommand`/`Complete` ucu için doğrulayıcı (validator) yok — `StartRideRequestValidator` gibi bir `CompleteRideRequestValidator` eklenebilir.
- `Wallet` ve `Tariff` domain tipleri henüz yazılmadı (Pricing/Billing context'leri ileriki günlerde gelecek).
- Migration dosyaları `Scootly.Infrastructure` projesinin kökünde duruyor, `Persistence/Migrations` altında değil (EF Core'un varsayılan davranışı, kozmetik bir fark, işlevsel sorun yok).
- Kapsam raporu (18. gün): Genel çizgi kapsamı %61, kritik domain sınıfları (Vehicle %88, Ride %75, GeoPoint %78, GeofenceEvaluator %87) hedefin (%80) civarında veya üzerinde. Result<T>, ExceptionHandlingMiddleware ve henüz kullanılmayan tipler (Reservation, NoParkingZone, olay sınıfları) düşük kapsamlı — bunlar ilgili özellikler yazıldığında doğal olarak artacak.

## Faz 2

### 21. gün — Identity

- **Veritabanı doğrulaması ertelendi.** `AddIdentity` migration'ı üretildi ve
  incelendi (yedi tablo, `identity` şeması, `EnsureSchema` çağrısı doğru) ama
  `dotnet ef database update` henüz çalıştırılmadı ve bir test kullanıcısıyla
  gerçek giriş denenmedi. Sebep: geliştirme makinesi Apple Silicon üzerinde
  Parallels ile çalışan bir Windows sanal makinesi; M3 öncesi Apple Silicon'da iç
  içe sanallaştırma olmadığı için Docker Desktop misafir işletim sisteminde
  kurulamıyor. Çözüm yolu `deploy/README.md` içinde yazılı (konteyneri macOS
  tarafında çalıştırıp Parallels paylaşılan ağ adresi üzerinden bağlanmak).
  Postgres ayağa kalkar kalkmaz uygulanacak.
- **user-secrets'taki bağlantı dizesi geçici bir yer tutucu.** Migration üretmek
  veritabanına bağlanmadığı için sorun çıkarmadı; `database update` öncesi gerçek
  değerle değiştirilecek.

### 22. gün — JWT

- **Yenileme (refresh) token'ı yok.** Erişim token'ının süresi dolunca yeniden
  giriş gerekiyor. Bilinçli sadeleştirme.
- **Kayıt ucu, e-postanın kayıtlı olduğunu sızdırıyor.** Identity `DuplicateEmail`
  hatası döndürüyor. Bundan kaçınmanın yolu kayıt isteğine her zaman aynı yanıtı
  verip doğrulama e-postası göndermektir; e-posta altyapısı bu projede yok.
  Giriş ucunda aynı sızıntı kapatıldı (bkz. ADR 0005, Karar 5).
- **Giriş ucunda hız sınırlama yok.** Hesap kilidi tek bir hesabı korur ama çok
  sayıda hesaba dağıtılmış denemeyi (password spraying) durdurmaz. Plan bunu
  Faz 3'e koyuyor.
- **`ExceptionHandlingMiddleware` 500 yanıtında `ex.Message` değerini istemciye
  yazıyor.** Bu, iç hata metinlerini (bağlantı dizesi parçaları, dosya yolları,
  SQL parçaları) dışarı sızdırabilen bir bilgi ifşası. 26. günkü OWASP taramasında
  düzeltilecek — o günün görevi zaten "en az bir gerçek açık bul ve düzelt".
- **`Scootly.Api/Controllers/Contracts/` klasörü ile `Scootly.Api.Contracts` ad
  alanı uyuşmuyor.** Devralınan tutarsızlık. Düzeltmek dosya taşımayı gerektiriyor
  ve şu an paralel geliştirilen dalla gereksiz çakışma üretir.

### Genel — Türkçe yerel ayarı ve büyük/küçük harf

Türkçe locale'de büyük `I` harfinin küçüğü noktasız `ı`'dır. Bu yüzden kültüre
duyarlı `ToLower()` / `ToUpper()` / `string.Compare` çağrıları Türkçe bir makinede
`"IDENTITY"` ile `"identity"`yi **eşleşmez** sayar. Yetkilendirmede böyle bir
karşılaştırma sessizce yanlış sonuç verir ve bu doğrudan bir güvenlik açığıdır.
Kural: kimlik, rol ve token karşılaştırmalarında her zaman
`StringComparison.Ordinal` kullanılacak. Bu hatayı geliştirme sırasında bir kabuk
betiğinde canlı olarak yaşadık (`grep -i identity` Türkçe locale'de eşleşmedi).

### 23. gün — rol ve iddia tabanlı yetki

- **`Reserve` ve `Start` uçlarında `DriverId` hâlâ istek gövdesinden okunuyor.**
  Rol kontrolü eklendi ama bu, sürücü A'nın gövdeye sürücü B'nin kimliğini
  yazarak B adına işlem yapmasını engellemiyor (OWASP A01 — yetkisiz nesne
  erişimi). 26. günde kimlik token'dan okunarak kapatılacak.
- **`Scootly.Api/Controllers/Contracts/` klasörü ile `Scootly.Api.Contracts` ad
  alanı uyuşmazlığı sürüyor.** Yeni eklenen `RegisterVehicleRequest` ve
  `VehicleCreatedResponse` de aynı yerde tutuldu — tutarlılık, tek seferlik
  düzeltmenin üreteceği çakışmaya tercih edildi.

### 24. gün — kaynak tabanlı yetki

- **`Complete` ucunda sürüş iki kez okunuyor:** bir kez controller'da yetki
  kontrolü için, bir kez handler içinde. EF Core'un değişiklik takibi sayesinde
  ikincisi genellikle veritabanına gitmez (aynı scope, aynı `DbContext`), ama bu
  bir garanti değil, bir yan etki. Temiz çözüm, yüklenen sürüşü handler'a
  parametre olarak geçirmek veya yetki kontrolünü handler'ın içine taşımaktır.
  İkincisi Application katmanına `IAuthorizationService` bağımlılığı sokar ve
  Karar 1'e (Application, ASP.NET'ten bağımsız olmalı) aykırıdır; birincisi
  komut imzasını bozar. Şimdilik ölçülebilir bir maliyeti olmadığı için
  bırakıldı, Faz 3'teki performans çalışmasında yeniden bakılacak.
- **404 / 403 ayrımı, kimliklerin rastgele Guid olmasına bağlı** (bkz. ADR 0007,
  Karar 4). Kimlik üretme biçimi değişirse bu karar da gözden geçirilmeli.
- **`IRegionScoped`'ı uygulayan gerçek bir alan sınıfı henüz yok.**
  `OperatorRegionHandler` yazıldı ve test edildi ama üretimde hiçbir kaynağa
  uygulanmıyor; ilk uygulayıcısı `FieldTask` olacak.

### 25.–30. gün

- **Cihaz kaydı için bir uç yok.** `device_credentials` tablosu ve
  `DeviceTokenService.HashSecret` var, ama cihaz ekleme/sır rotasyonu şu an
  yalnızca elle SQL ile yapılabiliyor. Yönetim ucu Faz 3'te cihaz simülatörüyle
  birlikte gelecek.
- **Cihaz token'ı iptal edilemiyor.** Sızan bir cihaz token'ı 15 dakika geçerli
  kalır. `jti` iddiası şimdiden konuyor; iptal listesi Faz 4'te.
- **Konum verisi saklama süresi kararı UYGULANMADI.** ADR 0009 doksan gün diyor;
  silme işini yapacak arka plan servisi Faz 3'te gelecek. Şu an hiçbir konum
  verisi silinmiyor.
- **Sürümsüz yollar (`/api/vehicles`, `/api/rides`) hâlâ açık.** 30. günde
  `/api/v1/...` eklendi, eskiler geriye dönük uyumluluk için bırakıldı.
  MVC arayüzü bağlandıktan sonra kaldırılacak.
- **`GET /api/vehicles` her istekte bir `COUNT` sorgusu çalıştırıyor.** Tablo
  büyüdüğünde bunun maliyeti sayfanın kendisinden yüksek olabilir; Faz 3'teki
  performans çalışmasında ölçülecek.
- **Hız sınırlama (rate limiting) hiçbir uçta yok.** Giriş, kayıt ve cihaz token
  uçları en kritikleri. Plan bunu Faz 3'e koyuyor.
- **Entegrasyon testleri 26. günün değişikliklerine göre güncellenmedi.**
  `RideEndpointsTests` hâlâ gövdede `DriverId` gönderiyor olabilir; Docker
  kurulmadığı için çalıştırılıp doğrulanamadı.

### Veritabanı doğrulaması — TOPLU BORÇ

21.–30. gün arasında yazılan hiçbir kod **çalışan bir veritabanına karşı
denenmedi**. Derleme temiz, birim testleri yeşil, migration'lar üretildi ve
incelendi; ancak `dotnet ef database update` çalıştırılmadı, hiçbir uca gerçek
bir istek atılmadı.

Sebep: geliştirme makinesi Apple Silicon üzerinde Parallels ile çalışan bir
Windows sanal makinesi ve M3 öncesi Apple Silicon'da iç içe sanallaştırma
olmadığı için Docker Desktop misafir işletim sisteminde kurulamıyor. Çözüm yolu
`deploy/README.md` içinde yazılı.

Doğrulanması gereken asgari liste:

1. `dotnet ef database update` — `identity` şeması ve sekiz tablo oluşuyor mu.
2. Test kullanıcısıyla `POST /api/auth/login` — token dönüyor mu.
3. Yanlış parola ile aynı uç — 401 ve **hiç kayıtlı olmayan e-posta ile
   birebir aynı yanıt** geliyor mu (22. günün asıl iddiası).
4. Beş yanlış denemeden sonra hesap kilitleniyor mu.
5. Sürücü A'nın token'ıyla sürücü B'nin sürüşünü bitirmeye çalışmak — 403.
6. Cihaz token'ıyla `/api/v1/vehicles` çağırmak — 403 (ara katman çalışıyor mu).

### 31.–40. gün — Faz 2 kapanışı

- **`system-design.md` içindeki ölçüm tablosu ve iki execution plan bloğu
  doldurulmalı.** `dotnet test tests/Scootly.DbLab --filter Gun33` çıktısındaki
  süreler ve planlar oraya yapıştırılacak. Kod tarafı tamam, belgeleme eksik.
- **`Scootly.DbLab` hermetik değil.** Çalışan PostgreSQL sunucusunu paylaşıyor
  ve kendi `scootly_lab` veritabanını oluşturuyor. CI'a geçildiğinde (Faz 5)
  Testcontainers'a dönülmeli; orada Docker zaten yerel olacak.
- **`GetByIdForUpdateAsync` üretim yolunda kullanılmıyor.** 39. günün deneyi
  için yazıldı. İlk gerçek kullanıcısı, çakışmanın sık olduğu bir işlem olacak
  (muhtemelen Faz 4'teki ödeme mutabakatı).
- **Sürüm damgası yalnızca `Vehicles` tablosunda.** `Rides` üzerinde eşzamanlı
  güncelleme senaryosu henüz yok; çıktığında aynı desen uygulanacak.
- **`Wallet` ve `Tariff` hâlâ yazılmadı.** 39. gündeki deadlock deneyi planın
  önerdiği `Vehicle`+`Wallet` yerine `Vehicles`+`Rides` ile yapıldı.
- **Saha Operatörü ve Denetçi rolleri hiçbir uçta kullanılmıyor.**
  `OperatorRegionHandler` yazıldı ve test edildi ama uygulanacağı kaynak
  (`FieldTask`) yok. Faz 2'nin "altı aktör doğrulanmış" hedefi bu yüzden
  kısmen karşılandı.
- **Mimari kural testi yok.** Katman ihlallerini yakalayacak test planın
  77. gününe ait, erken beklenmiyor.

### Kapanan borçlar

- ~~21.–30. gün arasındaki kod hiç çalıştırılmadı~~ — veritabanı kuruldu,
  migration'lar uygulandı, API ayağa kalktı, roller ve test kullanıcısı oluştu.
- ~~`ExceptionHandlingMiddleware` 500 yanıtında `ex.Message` sızdırıyor~~ —
  26. günde kapatıldı.
- ~~`Reserve` ve `Start` uçlarında `DriverId` gövdeden okunuyor~~ — 26. günde
  kapatıldı, `ContractShapeTests` geri gelmesini engelliyor.

## Faz 3 sonu (41.–60. gün)

### Bu fazda kapatılanlar

- ~~`Ride.StartedAt` hiçbir sütuna eşlenmemiş~~ — 41. günde düzeltildi.
  Salt okunur (`{ get; }`) özellikler EF Core kuralıyla eşlenmiyor; alan
  aylarca migration'a girmemişti ve `Ride.Complete` süreyi `endedAt -
  StartedAt` ile hesapladığı için **veritabanından okunan her sürüşün süresi
  iki bin yıl çıkardı**. Derleme temizdi, birim testler yeşildi — hiçbiri
  veritabanına gitmiyordu. `EslemeButunluguTests` artık modeli yansımayla
  gezip eşlenmemiş alan kalmadığını doğruluyor.
- ~~"Yakındaki araçlar" ucunda konum filtresi yok~~ — 42.–43. günde eklendi.
  Uç, adına rağmen tablodaki her aracı döndürüyordu.
- ~~`CompleteRide` ucu için doğrulayıcı yok~~ — `CompleteRideRequestValidator`
  yazıldı.
- ~~`Wallet` ve `Tariff` domain tipleri yok~~ — `Tariff` ve `Money` yazıldı.
  `Wallet` hâlâ yok (aşağıda).
- ~~`CancelReservationCommand` yazıldı ama handler'ı yok~~ — rezervasyonun
  süresi dolduğunda düşürülmesi 54. günün `ReservationTimeoutService`'ine
  geçti; `Vehicle.ReleaseReservation` alan kuralını tek yerde tutuyor.

### Faz 4'e taşınanlar

- **`GET /api/v1/vehicles` KIRICI biçimde değişti.** Enlem/boylam artık
  zorunlu ve yanıt tipi `NearbyVehicleResponse`. Sürümlemek yerine kırmayı
  seçtik: ucun tek bir istemcisi yok (MVC paneli 17. haftada, mobil hiç yok)
  ve yanlış davranışı bir sürüm numarasının arkasında dondurmak, ileride onu
  desteklemeye devam etmek demekti.
- **Harita önbelleği önek silerek geçersizleştiriliyor.** Bir aracın
  değişmesi bütün harita önbelleğini düşürüyor. Beş saniyelik TTL'de maliyeti
  düşük; trafik arttığında (saniyede onlarca kiralama) önbellek sürekli boş
  kalır. Bölgesel anahtarlama gerekecek (ADR 0017).
- **Cache stampede korumasız.** Aynı anahtar için aynı anda gelen N istek,
  ıska durumunda N kez veritabanına gidiyor. Ölçülüp karar verilecek.
- **Yakınlık sorgusu dikdörtgen, daire değil.** Köşelerde yarıçapın ~1,41
  katına kadar fazla kayıt dönüyor. Gerçek çözüm PostGIS; kurulmama gerekçesi
  ADR 0015'te.
- **`Scootly.Worker` iki kopya çalıştırılamaz.** Üç arka plan servisi de
  zamanlayıcıyla çalışıyor; iki kopya aynı rezervasyonu düşürmeye kalkar.
  Çözüm lider seçimi ya da 50. günün dağıtık kilidi.
- **Terk edilmiş sürüş tespiti süreye bakıyor, hareketsizliğe değil.**
  Doğrusu telemetriye bakmak olurdu; öyle yapılsaydı telemetri kesintisi
  "sürüş terk edildi"ye dönüşürdü.
- **`BatteryThresholdScanner` her turda aynı araçları buluyor.** Tekrarlı
  uyarıyı engelleyen bir durum yok. Kuyruk (13. hafta) gelmeden bunu çözmek,
  yanlış yere yazmak olurdu.
- **Telemetri yazımı başarısız olursa grup kaybediliyor**, yeniden
  denenmiyor. Gerekçe ve ödünleşim ADR 0019'da.
- **`TelemetryBulkWriter` tablo/sütun adlarını metin olarak biliyor.** Şema
  değişirse çalışma zamanında patlar. `Gun44_TopluYazmaTests` bunu yakalıyor
  ama derleme zamanı güvencesi yok.
- **Oran sınırlama ters vekil arkasında yanlış çalışır.** `RemoteIpAddress`
  vekilin adresini verir; `ForwardedHeaders` ara katmanı 20. haftada Nginx
  kurulurken eklenecek. Şimdi eklemek, doğrulanmamış bir `X-Forwarded-For`
  başlığına güvenmek olurdu.
- **`Wallet` ve ödeme akışı hâlâ yok.** `Tariff.CalculateFare` var ama onu
  çağıran bir yol yok — `Ride.Fare` hâlâ doldurulmuyor.
- **Sürümsüz yollar (`/api/vehicles`) hâlâ duruyor.** Kaldırma tarihi
  belirlenmedi.
- **Faz 3'ün ölçüm tabloları boş.** `docs/architecture/system-design.md`
  içindeki Faz 3 bölümünde 17 adet `(doldur)` var. Kod yazıldı, ölçüm
  yapılmadı — bu fazın asıl teslim ettiği şey sayılar olduğu için, bu borcun
  en önemlisi bu.
