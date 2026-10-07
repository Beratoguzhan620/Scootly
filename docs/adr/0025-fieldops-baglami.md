# ADR 0025: FieldOps Bağlamı — FieldTask Aggregate'i

## Durum
Kabul edildi.

## Bağlam
Plan dokümanı, 86, 104 ve 108. günlerde FieldTask (saha görevi) aggregate'inin
var olduğunu varsayıyordu, ama hiçbir zaman yazılmamıştı (bkz. ADR 0017,
76. gün tespiti). Bu, 86A (ek gün) olarak eklendi.

## Karar
`Scootly.Domain/FieldOps` altında yeni bir bağlam açıldı:
- `FieldTask : AggregateRoot` — Open → Assigned → Completed yaşam döngüsü
- `FieldTaskType`: BatteryReplacement, Inspection
- Veritabanı: araç + tür başına aynı anda yalnızca bir açık görev (kısmi
  benzersiz indeks + uygulama seviyesi kontrolü — çifte güvence)

## Entegrasyon Noktaları
- `BatteryLowConsumer` (Worker): artık yalnızca log yazmak yerine gerçek
  bir `CreateFieldTaskCommand(BatteryReplacement)` tetikliyor.
- `AbandonRideCommandHandler`: terk edilmiş sürüşten sonra araç bakıma
  alındığında, aynı transaction içinde bir `Inspection` görevi de açıyor.

## Kapsam Dışı Bırakılanlar (bilinçli)
- `FieldTask` domain olayları (`FieldTaskCreatedEvent` vb.) outbox'a
  eşlenmedi — şu an bu olayları dinleyen hiçbir dış bileşen yok.
  `DomainEventOutboxMapper`'ın kendi ilkesi ("yalnızca başka bir bileşenin
  tepki verdiği olaylar eşlenir") ile tutarlı.
- Api uçları (görev listeleme/üstlenme/tamamlama) bu günde eklenmedi —
  dokümanın kendisi "isteğe bağlı" olarak işaretlemişti, 86. günde
  (view component'ler, MVC FieldTasksController) ele alınacak.
- Fotoğraf yükleme (MinIO) — 108. günün konusu, bugünün kapsamı dışında.

## Test Kapsamı
- Domain: 8 test (yaşam döngüsü, kurallar, olaylar)
- Application: 5 test (CreateFieldTaskCommand idempotency dahil)
- Mevcut Riding testlerine 2 yeni test eklendi (Abandon → Inspection görevi)
- Consumer seviyesinde uçtan uca test yapılmadı (bkz. teknik borç listesi)