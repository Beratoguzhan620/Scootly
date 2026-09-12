# OWASP Taraması — 26. gün

Yöntem: her uç tek tek açıldı ve her biri için şu soru soruldu:

> Bu isteği yapan kullanıcı, URL'deki veya gövdedeki bir kimlik numarasını
> değiştirerek başkasının verisine erişebilir mi?

Tarama tarihindeki uç listesi ve bulgular aşağıda.

## Bulgu 1 — `POST /api/vehicles/{id}/reserve` (KRİTİK, düzeltildi)

**Sınıf:** OWASP A01 — Broken Access Control / Insecure Direct Object Reference

`ReserveVehicleRequest` içinde `DriverId` alanı vardı ve rezervasyon bu alana
göre yapılıyordu. Sürücü A, kendi geçerli token'ıyla giriş yapıp gövdeye sürücü
B'nin kimliğini yazarak **B adına** rezervasyon yapabiliyordu.

Kimlik doğrulaması vardı. Rol kontrolü (23. gün) vardı. İkisi de bu açığı
görmez, çünkü ikisi de "sen kimsin" sorusuna cevap veriyor; buradaki sorun
kullanıcının **kendi kimliğini beyan ediyor** olmasıydı.

**Düzeltme:** `DriverId` istek gövdesinden kaldırıldı. Sürücü kimliği artık
yalnızca `ICurrentUser.UserId` üzerinden, yani doğrulanmış token'dan okunuyor.
İstemcinin bu değeri etkilemesinin bir yolu kalmadı.

## Bulgu 2 — `POST /api/rides/start` (KRİTİK, düzeltildi)

Aynı sınıf, aynı sebep: `StartRideRequest` içinde `DriverId` vardı.

**Düzeltme:** Alan kaldırıldı, kimlik token'dan okunuyor.
`StartRideRequestValidator` içindeki `DriverId` kontrolü de kaldırıldı — artık
doğrulanacak bir şey yok, kimliğin geçerliliği token doğrulamasının kendisi.

## Bulgu 3 — `POST /api/rides/{id}/complete` (KRİTİK, 24. günde düzeltilmişti)

Sahiplik kontrolü hiç yoktu: giriş yapmış herhangi bir kullanıcı URL'deki
kimliği değiştirerek başkasının sürüşünü bitirebiliyordu.

**Düzeltme:** 24. günde kaynak tabanlı yetkilendirme (`SurusSahibi` politikası).

## Bulgu 4 — `ExceptionHandlingMiddleware` (ORTA, düzeltildi)

**Sınıf:** Bilgi ifşası (information disclosure)

500 yanıtında `ex.Message` doğrudan istemciye yazılıyordu. Beklenmeyen bir
istisnanın mesajı bizim yazdığımız bir cümle değildir: içinde bağlantı dizesi
parçası, dosya yolu, sunucu adı, SQL parçası veya kütüphane iç detayı olabilir.
Bunların her biri saldırgana sistemin haritasını çıkarmakta yardım eder.

**Düzeltme:** İstemci artık yalnızca bir referans numarası görüyor
(`context.TraceIdentifier`); ayrıntı sunucu tarafındaki loga yazılıyor.
Destek istendiğinde referans numarasıyla ilgili kayıt bulunabiliyor.
Ayrıca `Response.HasStarted` kontrolü eklendi: yanıt yazılmaya başlanmışsa
başlık değiştirmeye çalışmak ikinci bir istisna üretiyordu.

Alan kuralı ihlallerinin (`DomainException`) mesajı gösterilmeye devam ediyor —
o mesajlar kullanıcıya söylenmek üzere bizim yazdığımız cümleler.

## İncelenen ve sorun bulunmayan uçlar

| Uç | Değerlendirme |
|---|---|
| `POST /api/auth/register` | Rol gövdeden alınmıyor; her kayıt Driver. Yetki yükseltme yolu yok. |
| `POST /api/auth/login` | Üç başarısızlık durumu aynı yanıtı ve aynı süreyi veriyor (22. gün). |
| `GET /api/vehicles` | Ziyaretçiye açık, bilinçli. Sayfa boyutu 100 ile sınırlı (29. gün) — sınırsız olsaydı tek istekle tüm tablo istenebilirdi. |
| `POST /api/vehicles` | `SadeceYonetici` politikası. Kaynak sahipliği söz konusu değil, yeni kayıt oluşturuluyor. |
| `POST /api/v1/device-auth/token` | Cihaz yok / sır yanlış ayrımı yapılmıyor; zaman dengeleyici hash doğrulaması var. |

## Kalan riskler (kapatılmadı, kayıtlı)

- **Giriş ve cihaz token uçlarında hız sınırlama yok.** Hesap kilidi tek bir
  hesabı korur, çok sayıda hesaba dağıtılmış denemeyi (password spraying)
  durdurmaz. Plan bunu Faz 3'e koyuyor.
- **Kayıt ucu e-postanın kayıtlı olduğunu sızdırıyor.** Kaçınmanın yolu
  doğrulama e-postası göndermek; e-posta altyapısı bu projede yok.
- **`appsettings.Development.json` geçmişte açık metin parola içeriyordu.**
  Değer 21. günde kaldırıldı ama Git geçmişinde duruyor. Repo private kaldığı
  sürece düşük risk; public olacaksa geçmiş temizlenmeli ve o sırlar
  değiştirilmeli.
- **Token iptal mekanizması yok.** Sızan bir token süresi dolana kadar geçerli.
  `jti` iddiası bu amaçla şimdiden token'a konuyor.
