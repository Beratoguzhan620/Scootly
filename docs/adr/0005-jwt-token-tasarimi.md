# 0005 — JWT: içerik, doğrulama ve giriş davranışı

- **Durum:** Kabul edildi
- **Tarih:** 22. gün
- **Bağlam:** Kimlik altyapısı 21. günde kuruldu; bugün kimliğin isteklere nasıl
  taşınacağına karar veriliyor.

## Karar 1 — Token'a yalnızca yetki kararı için gereken bilgi konur

Token şunları taşır: kullanıcı kimliği (`sub`), tekil token kimliği (`jti`),
rol(ler) (`role`), ve varsa bölge (`home_region`). Başka hiçbir şey.

**Neden:** JWT'nin gövdesi **şifreli değildir**, yalnızca base64 ile kodlanmıştır.
İmza içeriğin *değiştirilmediğini* kanıtlar, *gizli kaldığını* değil. Token'ı eline
geçiren herkes içindeki her şeyi okuyabilir; tarayıcı konsoluna yapıştırılan bir
token da, bir log dosyasına düşen bir token da okunabilir durumdadır. Bu yüzden
e-posta, telefon, ad-soyad veya konum token'a konmaz.

`jti` bugün kullanılmıyor. İleride (Faz 4) tekrar oynatma tespiti ve iptal listesi
için gerekecek; token biçimi yayıldıktan sonra alan eklemek, eski token'ları
geçersiz kılmadan yapılamaz.

## Karar 2 — Kısa iddia adları, ve bunun bedeli

`sub` / `role` gibi kısa adlar kullanıldı. .NET'in varsayılanı bunları uzun
URI'lere çevirmektir (`http://schemas.microsoft.com/ws/2008/06/identity/claims/role`);
bu URI'ler her token'ı büyütür ve her istekte taşınır.

**Bedeli:** Doğrulama tarafında `MapInboundClaims = false`, `NameClaimType` ve
`RoleClaimType` açıkça ayarlanmak zorunda. Ayarlanmazsa `[Authorize(Roles = ...)]`
hiçbir rolü bulamaz ve her istek **sessizce 403 döner** — hata mesajı olmadığı için
teşhisi zor bir sınıf. Bu ayar `Program.cs`'te yorumla birlikte duruyor.

## Karar 3 — Kabul edilen algoritma tek ve sabit

`ValidAlgorithms = [HmacSha256]`.

**Neden:** Bu liste verilmezse doğrulayıcı, token'ın kendi başlığında yazan
algoritmaya bakar. Saldırganın bu alanı değiştirerek imza kontrolünü zayıflatmaya
veya atlatmaya çalıştığı saldırı sınıfına *algoritma karışıklığı* denir. Kabul
edilen algoritmayı sunucu tarafında sabitlemek bu sınıfı tamamen kapatır.

Aynı gerekçeyle `ClockSkew = TimeSpan.Zero`. Varsayılan 5 dakikadır: süresi dolmuş
bir token beş dakika daha kabul edilir. Tek makinede çalışan bir sistemde bu
toleransa gerek yok.

## Karar 4 — İmza anahtarı açılışta doğrulanır, uygulama gerekirse hiç başlamaz

`JwtOptions.Validate()` anahtarın var olduğunu ve en az 32 bayt (256 bit)
olduğunu kontrol eder; değilse uygulama açılışta istisna fırlatır.

**Neden:** Alternatif — eksik yapılandırmada sessizce bir varsayılana düşmek —
herkesin kendine yönetici token'ı üretebilmesi demektir. Çalışmayan bir uygulama
görünür bir arızadır; zayıf anahtarla çalışan bir uygulama görünmez bir açıktır.
32 bayt, HMAC-SHA256'nın hash çıktısının uzunluğudur; daha kısa anahtar, imzanın
güvenliğini hash fonksiyonunun sunduğu seviyenin altına indirir.

Anahtar user-secrets'ta durur, kaynak kodda ve `appsettings*.json` içinde değil.

## Karar 5 — Giriş başarısızlığı tek tip yanıt verir

"Kullanıcı bulunamadı", "parola hatalı" ve "hesap kilitli" durumlarının üçü de aynı
401 yanıtını ve aynı mesajı döner.

**Neden:** Farklı yanıtlar, saldırgana bir e-posta listesini deneyerek hangilerinin
kayıtlı olduğunu öğrenme imkânı verir (kullanıcı sayımı). "Hesap kilitli" demek de
aynı bilgiyi verir — üstelik hedefli kilitleme saldırısına davetiye çıkarır.

Mesajı gizlemek tek başına yetmez: kullanıcı hiç bulunamadığında da bir parola
hash doğrulaması çalıştırılıyor. Aksi halde "kullanıcı yok" yanıtı belirgin biçimde
daha hızlı döner ve **yanıt süresi** aynı bilgiyi sızdırır (zamanlama yan kanalı).

Hesap kilidi burada gerçekten uygulanıyor: yanlış parolada `AccessFailedAsync`,
başarılı girişte `ResetAccessFailedCountAsync`. Bu çağrılar olmadan 21. günde
tanımlanan kilit politikası yalnızca kâğıt üstünde kalırdı.

## Karar 6 — `CurrentUserAccessor` Api katmanında

Plan bu dosyayı Infrastructure/Identity altında öneriyor; biz `Scootly.Api/Identity`
altına koyduk.

**Neden:** Bu sınıfın okuduğu şey bir HTTP isteğidir. Infrastructure, kalıcılık ve
dış sistem katmanıdır; oraya `HttpContext` bağımlılığı taşımak, veritabanı kodunun
bir web isteğinin varlığını varsaymaya başlamasına açılan kapıdır. Token üretimi
(`JwtTokenGenerator`) kriptografi işidir ve Infrastructure'da kaldı.

## Bilinçli olarak yapılmayanlar

- **Yenileme (refresh) token'ı yok.** Süre dolunca yeniden giriş gerekiyor.
- **Kayıt ucu e-postanın kayıtlı olduğunu sızdırır.** Identity `DuplicateEmail`
  hatası döndürüyor. Bundan kaçınmanın yolu, kayıt isteğine her zaman aynı yanıtı
  verip doğrulama e-postası göndermektir; e-posta altyapısı bu projede yok. Bilinçli
  ödünleşme olarak kaydedildi.
- **Giriş ucunda hız sınırlama (rate limiting) yok.** Hesap kilidi tek bir hesabı
  korur ama çok sayıda hesaba dağıtılmış denemeyi (password spraying) durdurmaz.
  Plan bunu Faz 3'e koyuyor.
- **`Scootly.Api/Controllers/Contracts/` klasörü ile `Scootly.Api.Contracts`
  ad alanı uyuşmuyor.** Devralınan bir tutarsızlık; düzeltmek şu an Berat'ın dalıyla
  gereksiz çakışma üretir. Teknik borç listesinde.
