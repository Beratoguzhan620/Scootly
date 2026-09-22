# Kapsam Raporu

> 18. günde bir kez alınmıştı; 60. gün (Faz 3 checkpoint) güncellenmesini
> istiyor. Aşağıdaki tablo `f3.sh`'ın son adımının çıktısından doldurulacak —
> ya da elle:
>
> ```bash
> dotnet test --collect:"XPlat Code Coverage"
> # Sonuc: tests/<proje>/TestResults/<guid>/coverage.cobertura.xml
> # Kok elementteki line-rate degeri genel satir kapsami (0.61 = %61)
> ```

## Faz 3 sonu

| Proje | Satır kapsamı | Not |
|---|---|---|
| Scootly.Domain | _(doldur)_ | Hedef %80 — iş kurallarının yaşadığı yer |
| Scootly.Application | _(doldur)_ | |
| Scootly.Infrastructure | _(doldur)_ | |
| Scootly.Api | _(doldur)_ | |
| **Genel** | _(doldur)_ | 18. günde %61 idi |

## Kapsam sayısını nasıl okumalı

**Yüksek kapsam doğruluk demek değil.** Bir satırın çalıştırılmış olması, o
satırın doğru olduğunu göstermiyor; yalnızca test sırasında geçildiğini
gösteriyor. Bu projede bunun somut bir örneği var:

`Ride.StartedAt` hiçbir sütuna eşlenmemişti ve bu hata **birim testlerin
kapsamına giren satırlarda** duruyordu. Testler yeşildi, kapsam iyiydi, alan
sessizce kayboluyordu — çünkü hiçbir test veritabanına gitmiyordu. Hatayı
kapsam değil, iki dalın karşılaştırılması buldu.

**Düşük kapsam da tek başına kötü değil.** Henüz kullanılmayan tipler
(`Reservation`, `NoParkingZone`, olay sınıfları) doğal olarak düşük kapsamlı;
ilgili özellik yazıldığında kendiliğinden artacaklar. Onlar için bugün test
yazmak, davranışı değil mevcut kodu sabitlerdi.

## Faz 3'te kapsamı artıran/azaltanlar

**Artıranlar**

- `EslemeButunluguTests` — EF modelini yansımayla gezip eşlenmemiş alan
  arıyor. Kapsam sayısına az katkı, gerçek değere çok.
- `IdempotencyDeneyiTests` — 60. günün deneyinin dört durumu.
- Gün 56–59 ölçüm testleri (`Gun56`–`Gun59`) — veritabanı gerektirmiyorlar,
  her çalıştırmada koşuyorlar.

**Azaltanlar**

- Yeni yazılan ve henüz kullanılmayan kod: `RedisDistributedLock`
  (bilinçli olarak kullanılmıyor, ADR 0018), `IdempotentAttribute` /
  `IdempotencyFilter` (deneyin öznitelik tarafı, ADR 0020),
  `Tariff.CalculateFare` (çağıran bir yol henüz yok).
- `Scootly.Worker` ve `Scootly.DeviceSimulator` — iki yeni proje, hiç birim
  testi yok. Worker'ın üç servisi de veritabanına bağlı; onları test etmek
  `Scootly.DbLab` seviyesinde bir düzenek gerektiriyor. Teknik borç.

## Hedef

Kritik alan sınıfları (`Vehicle`, `Ride`, `GeoPoint`, `GeofenceEvaluator`,
`Money`, `Tariff`) için %80 ve üzeri. Genel sayı bir hedef değil, bir gösterge —
düşüyorsa bakılır, yükseliyor diye kutlanmaz.
