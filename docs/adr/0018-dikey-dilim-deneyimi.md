# ADR 0018: Vertical Slice Deneyimi — Katmanlı Mimari ile Karşılaştırma

## Durum
Deneysel, projeye entegre edilmedi.

## Bağlam
Dokümanın planladığı ClaimFieldTaskCommand özelliği projede yok (FieldOps
context'i henüz yazılmadı, bkz. ADR 0017). Bunun yerine gerçekten var olan
ReserveVehicle özelliği aynı deneyle karşılaştırıldı.

## Katmanlı Mimaride (Mevcut Yapı)
Bu özellik 5 tam dosyaya + 2 dosyanın bir kısmına yayılıyor, 3 proje arasında
(Domain, Application, Infrastructure, Api):
- ReserveVehicleCommand.cs, ReserveVehicleCommandHandler.cs (Application)
- IVehicleRepository.cs (Application), VehicleRepository.cs (Infrastructure)
- ReserveVehicleRequest.cs, VehiclesController.cs (Api)
- Vehicle.cs (Domain)

## Vertical Slice'ta (Deneysel)
Aynı özellik TEK bir dosyada, tek bir sınıfta toplanabiliyor
(docs/experiments/vertical-slice-reserve-vehicle/ReserveVehicleSlice.cs).

## Karşılaştırma

| Kriter | Katmanlı | Vertical Slice |
|---|---|---|
| Dosya sayısı | 7 (5 tam + 2 kısmi) | 1 |
| Bu özelliği anlamak için gereken gezinme | 3 proje arası atlama | Tek dosya, yukarıdan aşağı okuma |
| Domain kuralının korunması (encapsulation) | Vehicle.Reserve() iç kuralı korur, dışarıdan Status'a doğrudan erişim yok | Status'a DOĞRUDAN erişim gerekiyordu (varsayımsal) — domain kuralı slice içinde tekrar yazılırdı |
| Ortak kod (concurrency exception yönetimi, vb.) | Tek bir yerde (CompleteRideCommandHandler gibi diğer handler'larla paylaşılabilir desen) | Her slice kendi kopyasını tutar |
| Yeni bir geliştiricinin bu özelliği bulması | 7 dosya arasında arama | 1 dosyada her şeyi görür |

## Karar
Projenin genelinde katmanlı mimari (Clean Architecture) korunuyor — ADR 0001'de
verilen karar hâlâ geçerli. Bu deney, iki gerçek trade-off'u somut olarak
gösterdi:

1. **Vertical Slice, okunabilirlik için gerçekten daha hızlı** — özellikle
   yeni bir geliştiricinin tek bir özelliği anlaması için 1 dosya, 7 dosyadan
   çok daha az bilişsel yük gerektiriyor.
2. **Ama katmanlı mimari, domain kurallarının korunmasını (encapsulation)
   doğal olarak zorluyor** — Vehicle.Reserve() metodunun private set alanları,
   Status'un yanlışlıkla, kuralları atlayarak değiştirilmesini İMKANSIZ kılıyor.
   Vertical Slice'ta bu korumayı her slice'ın kendisi elle sağlamak zorunda
   kalırdı — insan hatasına daha açık.

Bu projenin ölçeğinde (çok sayıda karmaşık iş kuralı, uzun ömürlü bir kod
tabanı hedefi), katmanlı mimarinin sağladığı domain koruması, Vertical
Slice'ın okunabilirlik avantajından daha değerli görülüyor. Ama küçük,
CRUD-ağırlıklı, kısa ömürlü bir projede Vertical Slice tercih edilebilir.