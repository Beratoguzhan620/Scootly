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
