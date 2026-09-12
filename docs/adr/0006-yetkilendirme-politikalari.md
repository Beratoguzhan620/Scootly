# 0006 — Yetkilendirme: varsayılan kapalı, rol ve iddia politikaları

- **Durum:** Kabul edildi
- **Tarih:** 23. gün
- **Bağlam:** Token 22. günde geldi; artık "bu isteği kim yapıyor" sorusunun
  güvenilir bir cevabı var. Bugün "bu kişi bunu yapabilir mi" sorusu kuruluyor.

## Karar 1 — Varsayılan politika kimlik doğrulamasını zorunlu kılar

`SetFallbackPolicy(RequireAuthenticatedUser())`. Yetkilendirme meta verisi
taşımayan her uç, kimlik doğrulaması ister. Açık bırakmak isteyen uç
`[AllowAnonymous]` yazmak zorunda.

**Neden:** Alternatif, her ucu tek tek `[Authorize]` ile işaretlemektir — yani
korumayı **opt-in** yapmaktır. O modelde yeni eklenen bir uç, birileri işareti
koymayı unuttuğu anda sessizce herkese açık olur ve bu unutkanlık hiçbir hata
üretmez; derleme geçer, testler yeşil kalır, açık yalnızca birileri fark edene
kadar durur. Burada tersi geçerli: yeni uç varsayılan olarak **kapalıdır**, açmak
bilinçli ve gözden geçirmede görünen bir satır gerektirir.

Bu kararın bedeli, `[AllowAnonymous]` yazmayı unutan bir ucun 401 dönmesidir —
görünür, teşhisi kolay bir arıza. Ters yöndeki hata ise görünmez bir açıktır.

Bugün iki muafiyet var, ikisi de açıkça yazılı:

- `AuthController` — henüz token'ı olmayan biri token almak için buraya gelir.
- `VehiclesController.GetNearby` — plandaki altı aktörden biri "Ziyaretçi";
  kaydolmaya değip değmeyeceğine yakınında araç olup olmadığına bakarak karar
  verir.

## Karar 2 — Politika adları sabit, serbest metin değil

`PolicyNames` sınıfında sabitler. Politika adını yanlış yazmak derleme hatası
vermez; çalışma zamanında istisna fırlatır ve bu istisna yalnızca o uç
çağrıldığında ortaya çıkar — test edilmemiş bir uçta aylarca fark edilmeyebilir.

## Karar 3 — Politikalar ayrı bir dosyada, Program.cs'te değil

`Api/Authorization/AuthorizationRegistration.cs`.

**Neden:** Test edilebilirlik. Politikaların gerçekten doğru kararı verdiği artık
web sunucusu, veritabanı ve Docker olmadan sınanabiliyor: servis koleksiyonu elde
kuruluyor, `IAuthorizationService` sorgulanıyor. `Scootly.Api.UnitTests`
projesindeki 14 test bunu yapıyor.

Testlerde `ClaimsIdentity` kurulurken `nameType` ve `roleType` değerleri,
`Program.cs`'teki `TokenValidationParameters` ile **aynı** verilmek zorunda.
Farklı olsalardı testler geçer ama gerçek istekler 403 dönerdi.

## Karar 4 — Rol tabanlı ve iddia tabanlı politikalar ayrı ayrı var

Dört rol politikası (`SadeceYonetici`, `SadeceOperator`, `SadeceSurucu`,
`SadeceDenetci`) ve bir iddia politikası (`BolgeliPersonel`).

**Neden:** Rol politikası "sen kimsin" sorusuna cevap verir; iddia politikası
"hangi niteliğe sahipsin" sorusuna. `BolgeliPersonel`, kullanıcının bir
`home_region` iddiası taşımasını şart koşar ama **değerine bakmaz**. Değeri
erişilen kaynakla karşılaştırmak üçüncü bir adımdır — kaynak tabanlı yetki —
ve 24. günde gelecek. Üçünün de ayrı ayrı yazılmış olması, aralarındaki farkın
kod üzerinden görülmesini sağlıyor.

## Bugün BİLEREK kapatılmayan açık

`Reserve` ve `Start` uçlarına rol kontrolü eklendi ama `DriverId` hâlâ istek
gövdesinden okunuyor. Yani sürücü A, gövdeye sürücü B'nin kimliğini yazarak B
adına işlem yapabilir. `Complete` ucunda ise sahiplik kontrolü hiç yok: giriş
yapmış herhangi biri, URL'deki kimliği değiştirerek başkasının sürüşünü
bitirebilir.

Bu, rol tabanlı yetkinin nerede yetersiz kaldığının somut örneği: "bu bir sürücü
mü" sorusuna cevap veriyor, "bu sürücü BU kaynağa erişebilir mi" sorusuna
veremiyor. İkinci soru kaynağın kendisine bakmayı gerektiriyor.

Üç uç da controller'larda yorumla işaretlendi ve teknik borç listesinde kayıtlı.
24. günde kaynak tabanlı yetkiyle, 26. gündeki OWASP taramasında da kimliğin
token'dan okunmasıyla kapatılacak.
