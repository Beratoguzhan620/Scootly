# ADR 0016 — Sorgu takip davranışı varsayılan olarak kapalı

- **Durum:** Kabul edildi
- **Gün:** 41
- **Bağlam:** Faz 3, Hafta 9 — EF Core performansı

## Karar

`ScootlyDbContext.OnConfiguring` içinde
`UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking)` ayarlandı.
Değiştirilecek varlığı okuyan yollar (repository'ler) açıkça `AsTracking()`
diyor.

## Sorun

EF Core'un değişiklik izleyicisi (change tracker), okunan her varlığın bir
kopyasını bellekte tutar. `SaveChanges` çağrıldığında hangi alanların
değiştiğini bu kopyayla karşılaştırarak bulur.

Yazma senaryosunda bu mekanizmanın tamamı gereklidir. Okuma senaryosunda hiçbir
parçası gerekli değildir: iki kat bellek, satır başına bir izleme girdisi ve
hiç kullanılmayacak bir karşılaştırma altyapısı.

## Değerlendirilen seçenekler

### 1. Her okuma sorgusuna `AsNoTracking()` eklemek

Yaygın olan bu. Reddedildi, çünkü **unutmanın cezası sessiz**. Unutulan sorgu
çalışmaya devam eder, yalnızca yavaşlar ve daha çok bellek harcar. Bunu ancak
yük testinde, hem de aramayı bilerek yaparsan fark edersin. Kod incelemesinde
"burada `AsNoTracking` eksik" demek, incelemecinin her sorguyu tek tek
denetlemesini gerektirir.

### 2. Varsayılanı takipsiz yapmak, yazma yollarının açıkça takip istemesi

Seçilen. Unutmanın cezası burada **gürültülü**: takip isteyen bir yol
`AsTracking()` demeyi unutursa, nesne değiştirilir ama `SaveChanges` hiçbir
değişiklik görmez ve **sıfır satır** yazar. Bu, ilk testte kırmızı olur.

Yani iki seçenek arasındaki fark performans değil, **hatanın nasıl ortaya
çıktığı**. Sessizce yavaşlayan bir sistem ile gürültülü biçimde kırılan bir
test arasında ikincisi tercih edilir.

### 3. Sorgu tiplerini birbirinden ayırmak (CQRS ile ayrı bağlam)

Okuma için ayrı bir `DbContext` (takipsiz), yazma için ayrı (takipli).
Reddedildi: bu projenin bugünkü boyutunda iki bağlamın bakım maliyeti,
çözdüğü problemden büyük. Faz 4'te okuma modeli gerçekten ayrışırsa yeniden
değerlendirilecek.

## Sonuçlar

**Olumlu**

- Bütün okuma yolları, kimse hatırlamak zorunda kalmadan takipsiz.
- Takip isteyen yerler kodda görünür: `AsTracking()` araması, "bu sorgu bir
  şeyi değiştirecek" diyen yerlerin tam listesini veriyor.

**Olumsuz**

- EF Core'un varsayılanını bilen biri için şaşırtıcı. Bu yüzden karar hem
  `OnConfiguring`'in belgesinde hem burada yazılı.
- `AsTracking()` demeyi unutan yeni bir yazma yolu, derleme hatası değil test
  hatası verir. Derleme zamanında yakalamanın bir yolu yok.

## Etkilenen yerler

- `VehicleRepository.GetByIdAsync` — `AsTracking()`
- `VehicleRepository.GetByIdForUpdateAsync` — `AsTracking()` (adı "ForUpdate"
  olan bir metodun takipsiz dönmesi, kilidi alıp hiçbir şey yazmamak olurdu)
- `RideRepository.GetByIdAsync` — `AsTracking()`

## Ölçüm

41. günün ölçümü `docs/architecture/system-design.md` içindeki
"Takip maliyeti" tablosunda; testi `tests/Scootly.DbLab/Gun41_TakipMaliyetiTests.cs`.
