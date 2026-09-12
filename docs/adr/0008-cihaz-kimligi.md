# 0008 — Araç cihazlarının kimliği

- **Durum:** Kabul edildi
- **Tarih:** 25. gün

## Bağlam

Araç cihazları insan kullanıcı gibi giriş yapamaz: parolaları, e-postaları,
tarayıcıları yoktur. OAuth 2.0'ın bu durum için tanımladığı akış "client
credentials": istemci kendi kimliği ve sırrıyla doğrudan token alır.

## Karar 1 — Cihaz sırrı hash'lenerek saklanır

`DeviceCredential.SecretHash`, kullanıcı parolalarıyla aynı hash'leyici ile.

**Neden:** Cihaz sırrı bir paroladan farklı değildir. Veritabanı sızarsa düz
metin saklanmış bir sır, saldırganın o cihaz adına token alması için yeterlidir.

Cihaz bulunamadığında da bir hash doğrulaması çalıştırılıyor — giriş ucundaki
ile aynı gerekçe: yanıt süresi hangi cihaz kimliklerinin kayıtlı olduğunu
sızdırmasın. Cihaz kimlikleri genelde tahmin edilebilir bir düzende (plaka,
seri numarası) olduğu için buradaki sayım riski kullanıcı e-postalarından
yüksektir.

## Karar 2 — Cihaz token'ının hedef kitlesi farklı

`aud = scootly-devices`, kullanıcı token'ında `scootly-clients`.

**Neden:** Aynı hedef kitle kullanılsaydı, cihaz sırrı sızan bir saldırgan cihaz
token'ıyla kullanıcı uçlarına da gidebilirdi. Ayrı hedef kitle, bunu doğrulama
seviyesinde imkânsız kılıyor.

## Karar 3 — Token ömrü 15 dakika, kullanıcıda 60

**Neden:** Cihazlar sahada, fiziksel erişime açık yerlerde duruyor. Token iptal
edilemediği için sızan bir token süresi dolana kadar geçerli; tek koruma o
pencereyi daraltmak.

## Karar 4 — Ayrı bir doğrulama hattı DEĞİL, aynı hattın kısıtlanmışı

Cihaz token'ı da normal JWT hattından geçiyor. Üstüne
`DeviceTokenScopeMiddleware` ekleniyor: cihaz token'ı taşıyan bir istek
`/api/v1/devices` (ve token alma ucu) dışına çıkarsa 403.

**Neden:** Paralel ikinci bir kimlik doğrulama hattı, hata yapılabilecek iki yer
demektir. Tek hat + bir kısıt, iki hattan daha az yüzey açıyor. Ara katman
ikinci savunma hattı: hedef kitle yapılandırması ileride yanlışlıkla
gevşetilirse yol kısıtı hâlâ yerinde durur.

## Karar 5 — `VehicleDevice` bir Identity rolü değil

Sabit olarak tanımlı ama `RoleNames.All` listesinde yok, yani Identity
tablolarına bir satır olarak eklenmiyor.

**Neden:** Cihazlar kullanıcı değil. Identity'ye bir "cihaz rolü" satırı
eklemek, o rolün yanlışlıkla bir insana atanabilmesi demek olurdu.

## Ara katman sırası

`UseAuthentication` → `DeviceTokenScopeMiddleware` → `UseAuthorization`.

Önce olsaydı `context.User` henüz dolmamış olur, her isteği "cihaz değil" sayar
ve **hiçbir şeyi engellemezdi** — üstelik bunu fark etmezdik, çünkü
başarısızlığı sessiz olurdu. Sonra olsaydı yetki kararı zaten verilmiş olurdu.
