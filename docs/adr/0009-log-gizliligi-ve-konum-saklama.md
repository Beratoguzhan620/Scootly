# 0009 — Log gizliliği ve konum verisi saklama süresi

- **Durum:** Kabul edildi
- **Tarih:** 27. gün

## Karar 1 — Loglara giden nesnelerde hassas alanlar maskelenir

`SensitiveDataDestructuringPolicy`, adı `password`, `parola`, `secret`, `sir`,
`token`, `signingkey`, `apikey`, `authorization` veya `connectionstring`
parçalarından birini **içeren** her özelliği `***` ile değiştiriyor.

**Neden:** Loglar genellikle en az korunan veri kaynağıdır — birçok kişinin
erişimi olur, uzun süre saklanır, yedeklenir, çoğu zaman şifrelenmez. Bir
token'ın loga düşmesi, o token'ın süresi dolana kadar geçerli bir açık demektir;
ve bu açık kodu okuyarak değil ancak logları okuyarak fark edilir.

Tam eşleşme yerine "içerir" kullanılıyor: `Password`, `NewPassword`,
`PasswordHash` ve `ConfirmPassword` tek kuralla kapsanıyor.

**Bu bir güvenlik ağıdır, birincil koruma değil.** Birincil koruma hassas veriyi
zaten loglamamaktır. Ağın işlevi, birinin ileride `LogInformation("{@Request}")`
yazması durumunda zararı sınırlamak.

Karşılaştırma `OrdinalIgnoreCase`. Kültüre duyarlı olsaydı Türkçe yerel ayarında
`APIKey` gibi bir ad kaçırılabilir ve maskeleme **sessizce** devre dışı kalabilirdi.

Kural yalnızca `Scootly` ile başlayan ad alanlarındaki tiplere karışıyor:
kütüphane tiplerini yansımayla gezmek hem pahalı hem öngörülemez (bazı
özellikler okunduğunda yan etki üretir veya istisna fırlatır).

## Karar 2 — İstek logları başlık içermez

`UseSerilogRequestLogging` yöntem, yol, durum kodu ve süreyi yazıyor; başlıkları
yazmıyor. `Authorization` başlığı tam da bu yüzden.

## Karar 3 — Konum verisi 90 gün saklanır

Sürüş başlangıç ve bitiş koordinatları, sürüşün tamamlanmasından **90 gün**
sonra silinecek veya kaba bir bölge bilgisine indirgenecek.

**Neden:** Konum verisi, bir kullanıcı hakkında en çok şey söyleyen veri
türlerinden biri — nerede yaşadığı, nerede çalıştığı, ne zaman evde olmadığı.
Sonsuza kadar saklamanın iki maliyeti var: saklanan her gün, sızıntı durumunda
açığa çıkacak veri miktarını artırıyor; ve bir veri hiç silinmiyorsa, silinme
politikası olmadığı için değil, kimse düşünmediği için saklanıyor demektir.

90 gün seçildi çünkü:

- Fatura itirazları ve kayıp eşya taleplerinin pratik üst sınırı bu aralıkta.
- Faz 3'teki analitik çalışması için yeterli geçmiş bırakıyor.
- Bir yılın çok altında; sızıntı halindeki hasarı sınırlıyor.

**Uygulanmadı.** Silme işini yapacak arka plan servisi Faz 3'te gelecek
(plan 11. hafta). Karar şimdi kayda geçiyor çünkü veri şimdiden birikiyor ve
"ne kadar saklayacağız" sorusunu veri büyüdükten sonra sormak, cevabı
"silmek pahalı, kalsın" yapıyor. Teknik borç listesinde açık madde olarak duruyor.
