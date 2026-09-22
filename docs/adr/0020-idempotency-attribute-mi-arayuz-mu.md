# ADR 0020 — Tekrara dayanıklılık: öznitelik değil, arayüz

- **Durum:** Kabul edildi (yöntem olarak; henüz hiçbir uca uygulanmadı)
- **Gün:** 60 — Faz 3 checkpoint deneyi
- **Bağlam:** Faz 3 kapanışı

## Deney

Aynı iş iki yoldan yazıldı ve ikisi de depoda duruyor:

| Yaklaşım | Dosyalar |
|---|---|
| **Öznitelik** | `src/Scootly.Api/Idempotency/IdempotentAttribute.cs`, `IdempotencyFilter.cs` |
| **Arayüz** | `src/Scootly.Application/Abstractions/IIdempotentCommand.cs`, `Behaviors/IdempotencyBehavior.cs` |

İkisi de aynı şeyi yapıyor: bir işlemin anahtarını hatırlayıp aynı anahtarla
gelen ikinci isteği çalıştırmamak.

## Neden bu sistemde gerekli

Teorik bir ihtiyaç değil. 53. günün cihaz simülatörü ağ hatasında yeniden
deniyor; mobil istemci de deneyecek. "Rezervasyon oluştur" isteği iki kez
ulaşırsa ikinci istek yeni bir rezervasyon üretmemeli — ve bu, kullanıcının
iki araç kiralaması demek.

## Karar: arayüz

### 1. Öznitelik yalnızca HTTP hattını koruyor

Belirleyici sebep bu. `IdempotencyFilter` bir MVC süzgeci; yalnızca bir
controller eylemi çağrıldığında çalışıyor.

Ama 54. günden beri bu sistemde komutları çağıran **ikinci bir yol** var:
`Scootly.Worker`. `ReservationTimeoutService` ve `AbandonedRideDetector`
doğrudan alan modelini ve repository'leri kullanıyor, hiçbir controller'dan
geçmiyor. Öznitelik tabanlı koruma orada **yok** — ve olmadığı hiçbir yerde
görünmüyor.

Arayüz Application katmanında olduğu için her iki yolda da geçerli.

### 2. Derleme zamanında görünür olmak

Öznitelikte bir ucun korunup korunmadığı ancak yansımayla, çalışma zamanında
bilinebiliyor. Özniteliği yazmayı unutan uç hiçbir uyarı üretmeden korumasız
kalıyor.

Arayüzde aynı soru tip sisteminde cevaplanıyor: `IIdempotentCommand`
uygulayanları IDE tek tuşla listeliyor, ve `IdempotencyBehavior.CalistirAsync`
generic kısıtı sayesinde arayüzü uygulamayan bir komutla **derlenmiyor**.

Bu, Faz 3 boyunca üç kez aynı yere varan muhakemenin devamı: ADR 0016'da takip
varsayılanı, ADR 0019'da atılan telemetri kayıtlarının sayılması. Her seferinde
asıl soru "hangisi daha hızlı" değil, **"yanlış yapıldığında nasıl fark
edilir"** oldu.

### 3. Yansımanın maliyeti belirleyici DEĞİL

60. günün girişi yansımanın performans maliyetinden bahsediyor ve bu doğru —
ama burada geçerli değil: ASP.NET Core öznitelik keşfini uç oluşturulurken bir
kez yapıp önbelleğe alıyor. Özniteliği bu sebeple reddetmek, ölçmeden karar
vermek olurdu.

## Özniteliğin daha iyi olduğu yer

Dürüst olmak gerekirse öznitelik bir konuda üstün: **uç başına
yapılandırma**. `[Idempotent(HatirlamaSuresiDakika = 5)]` yazmak, aynı bilgiyi
komut tipine gömmekten okunaklı. Arayüzde süre tek bir sabit
(`IdempotencyBehavior.HatirlamaSuresi`).

Bu sistemde farklı uçların farklı hatırlama süresine ihtiyacı yok; olsaydı
karar değişebilirdi.

## Uygulanmadı — neden

Deney yazıldı, karar verildi, ama hiçbir komut `IIdempotentCommand`
uygulamıyor ve `IIdempotencyStore`'un bir uygulaması yok.

Sebep: doğru uygulama Redis üzerinde `SET NX` ile olmalı (bellek içi bir depo
iki API kopyasında yanlış çalışır — 46. günün dersi), ve o yol 47. günde
kuruldu ama **iki kopyayla doğrulanmadı**. Doğrulanmamış bir altyapının
üstüne doğruluk garantisi kurmak, ADR 0018'de dağıtık kilit için
reddettiğimiz şeyin aynısı olurdu.

Sıra: önce Redis'i iki kopyayla doğrula, sonra `IIdempotencyStore`'u onun
üstüne yaz, sonra rezervasyon ve sürüş başlatma komutlarına uygula. Teknik
borç listesinde.

## Sınanan

`tests/Scootly.Api.UnitTests/IdempotencyDeneyiTests.cs`:

- Aynı anahtarla ikinci çağrı komutu **hiç çalıştırmıyor** (yalnızca dönen
  değer farklı olsaydı yan etki yine iki kez oluşurdu).
- Farklı anahtarlar ayrı işlem sayılıyor.
- Anahtar komut tipiyle kapsamlanıyor: aynı anahtarın iki farklı komutta
  kullanılması alakasız bir komutu engellemiyor.
- Boş anahtar sessizce "koruma yok"a dönüşmüyor, hata veriyor.
