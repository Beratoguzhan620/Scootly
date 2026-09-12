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
