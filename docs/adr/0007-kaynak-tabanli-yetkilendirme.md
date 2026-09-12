# 0007 — Kaynak tabanlı yetkilendirme

- **Durum:** Kabul edildi
- **Tarih:** 24. gün
- **Bağlam:** 23. günde rol ve iddia politikaları kuruldu. İkisi de kullanıcıya
  bakıp karar veriyor. Bugünkü soru farklı: "bu kullanıcı BU kaynağa erişebilir mi".

## Neden öznitelik yetmiyor

`[Authorize(Policy = ...)]` özniteliği, istek daha hiçbir şey okumadan çalışır.
Elinde kullanıcı vardır, kaynak yoktur. "Bu sürüş bu sürücünün mü" sorusunun
cevabı ise sürüşün kendisine bakmadan verilemez.

Bu yüzden kaynak tabanlı kurallar controller içinde, kaynak yüklendikten sonra
çağrılıyor:

```csharp
var ride = await _rides.GetByIdAsync(id, cancellationToken);
if (ride is null) return NotFound();

var yetki = await _authorization.AuthorizeAsync(User, ride, PolicyNames.SurusSahibi);
if (!yetki.Succeeded) return Forbid();
```

## Karar 1 — Gereksinim ve kural ayrı sınıflar

`RideOwnerRequirement` boş bir işaret; mantık `RideOwnerHandler` içinde.

**Neden:** Bir gereksinim, kuralın **adıdır**, mantığı değil. Bu ayrım sayesinde
aynı gereksinimi karşılayabilecek birden fazla kural yazılabilir. İleride
"denetçi rolündeki kullanıcı her sürüşe bakabilir" diye ikinci bir handler
eklenirse, politika tanımına hiç dokunulmadan çalışır.

## Karar 2 — Başarısızlıkta `context.Fail()` çağrılmıyor

Handler, koşul sağlanmazsa hiçbir şey yapmadan çekiliyor.

**Neden:** `Fail()`, aynı gereksinimi karşılayabilecek başka bir handler başarılı
olsa bile kararı **kesin olarak** olumsuz yapar. İstenen bu değil: yukarıdaki
denetçi kuralı eklendiğinde, sahiplik kuralının onu bloke etmemesi gerekiyor.
Hiçbir handler `Succeed` demezse sonuç zaten olumsuzdur — yani sessizce çekilmek
güvenli tarafta kalmaktır.

`Fail()` yalnızca "hiçbir koşulda geçilmemeli" denmek istendiğinde kullanılmalı
(örneğin askıya alınmış bir hesap).

## Karar 3 — Bölge karşılaştırması `OrdinalIgnoreCase`

`CurrentCultureIgnoreCase` **değil**.

**Neden:** Türkçe yerel ayarında büyük `I` harfinin küçüğü noktasız `ı`dır.
Kültüre duyarlı karşılaştırma kullanılsaydı, `"ISTANBUL"` ile `"istanbul"`
Türkçe bir makinede eşleşmez, İngilizce bir makinede eşleşirdi — yani
**yetkilendirme kararı, kodun çalıştığı makinenin dil ayarına göre değişirdi.**
Yetkilendirmede makineye göre değişen bir karar, tanımı gereği bir açıktır.

Bu tuzağı geliştirme sırasında canlı olarak yaşadık: bir kabuk betiğindeki
`grep -i identity` Türkçe locale'de `AddIdentity` dosyasını bulamadı. Aynı sınıf
hata C#'ta `ToLower()` / `ToUpper()` / kültüre duyarlı `string.Equals` ile ortaya
çıkar. `ResourcePolicyTests` içindeki üç satırlık `[Theory]` bunu kilitliyor.

## Karar 4 — Sürüş yoksa 404, başkasınınsa 403

Bu, güvenlik açısından tartışmalı bir tercih ve bilinçli verildi.

Var olmayan kaynak için 404, başkasının kaynağı için 403 dönmek, saldırgana
"bu kimlik numarası var" bilgisini verir. Kimlik numaraları sıralı olsaydı
(1, 2, 3…) bu bilgi bir sayım aracına dönüşürdü ve iki durumun da 404 dönmesi
gerekirdi — 22. gündeki giriş ucunda tam olarak bunu yaptık.

Burada kimlikler rastgele `Guid`. 128 bitlik uzayda tahmin pratikte imkânsız
olduğu için sızan bilginin değeri yok; buna karşılık 403 ile 404 arasındaki
ayrımın teşhis değeri gerçek ("yanlış kimlik mi yazdım, yoksa yetkim mi yok").

**Bu karar kimlik üretme biçimine bağlıdır.** Kimlikler herhangi bir noktada
sıralı hale gelirse, bu karar da geri alınmalıdır.

## Karar 5 — `IRegionScoped` Domain katmanında

Operasyon bölgesi bu sistemde bir alan kavramıdır, bir web kavramı değil.
Arayüz Api katmanında olsaydı, alan sınıflarının web katmanına bakması gerekirdi
ve bağımlılık yönü tersine dönerdi.

İlk gerçek uygulayıcısı `FieldTask` olacak; o alan modeli henüz yazılmadı. Kural
şimdiden yazıldı ve test edildi, çünkü **kuralın doğruluğu kaynağın varlığına
bağlı değil** — testte bir test ikizi (`SahaGorevi`) kullanılıyor.

## Bugün kapanan ve kapanmayan

| Uç | Durum |
|---|---|
| `POST /api/rides/{id}/complete` | **Kapandı** — sahiplik kontrolü eklendi |
| `POST /api/rides/start` | Açık — `DriverId` gövdeden geliyor |
| `POST /api/vehicles/{id}/reserve` | Açık — `DriverId` gövdeden geliyor |

Kalan iki uçtaki eksik, sahiplik değil **kimlik kaynağı**: ortada henüz bir sürüş
yok, dolayısıyla sahibi de yok. Sorun, sürücünün kendi kimliğini beyan ediyor
olması. Çözümü kaynak tabanlı yetki değil, kimliği token'dan okumak — 26. günün
işi.
