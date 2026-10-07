# ADR 0027: MVC Tarayıcı Kodu → Api Kimlik Kararı

## Durum
Kabul edildi. (Numaralandırma notu: dokümanın önerdiği 0025, 86A'da FieldOps
bağlamına, 0026 ise 87. günde arayüz yetkilendirmesine verildiği için bu
karar 0027 olarak kaydedildi.)

## Bağlam
Mvc'nin tarayıcıda çalışan JavaScript'i (harita, ileride SignalR), Api'ye
istek atacak. İki seçenek vardı:
(a) Api'ye cookie şemasını da tanıtmak
(b) Sayfa render edilirken kısa ömürlü bir JWT üretip JS'e gömmek

## Karar
**(b) seçildi.** Api tek şemada (JWT) kalıyor — cookie/CSRF yüzeyi Api'ye
hiç taşınmıyor, 82. günde kurduğumuz "Api = JWT, Mvc = Cookie" ayrımı
bozulmuyor.

## 88. Gün Kapsamı
Bugünkü harita özelliği (`GET /api/v1/vehicles`) zaten **anonim** bir uç,
token gerektirmiyor — bu yüzden bugün gerçek bir token üretimi yapılmadı.

## 89. Gün İçin Hazırlık
`FleetHub` (SignalR) kimlik istediği için, 89. günde `MapController`'a
kısa ömürlü (5-15 dk) bir JWT üretme eklenecek — `JwtTokenGenerator`
(zaten Infrastructure'da var, `AddScootlyJwtTokens()` ile) kullanılacak.
Token, sayfaya gömülecek (örnek: bir `<script>` değişkeni), **localStorage'a
yazılmayacak** — bu, dokümanın açık uyarısı.

## CORS
Mvc origin'i (`http://localhost:5096`) Api'nin `Cors:AllowedOrigins`'ine
eklendi (`appsettings.Development.json`). 99. günde Nginx ile aynı origin
olunca bu ayar gereksizleşecek, kaldırılmayacak (ortam bazlı bırakılacak).