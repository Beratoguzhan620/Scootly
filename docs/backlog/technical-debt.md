# Teknik Borç Listesi

Bu dosya, bilinçli olarak şimdi düzeltilmeyen ama fark edilen eksiklikleri kaydeder. Kapatılan maddeler silinmez;
nasıl kapatıldığıyla birlikte aşağıdaki geçmiş bölümünde tutulur.

## Açık borçlar

| Konu | Açıklama | Neden ertelendi |
|---|---|---|
| Git geçmişinde eski sırlar | 28. gün öncesi commit'lerde eski JWT anahtarı, cihaz sırrı ve DB parolası duruyor. Hepsi 27.09.2026'da yenilendi, artık geçersiz. | Geçmişi yeniden yazmak (`git filter-repo`) paylaşılan dalları bozar; rotasyon sızıntının etkisini zaten ortadan kaldırdı (ADR 0021). |
| Araç başına cihaz kimliği | Cihazlar ağ geçidi modeliyle (tek istemci, çok araç) doğrulanıyor. | Provizyon ve sır dağıtımı gerektirir; gerçek cihaz filosuna geçişte değerlendirilecek (ADR 0021). |
| DLQ izleme / alarm | Ölü mektup kuyruklarına düşen mesajlar yalnızca RabbitMQ yönetim arayüzünden görülebiliyor. | Gözlemlenebilirlik altyapısı (metrik, alarm) henüz yok. |
| Sürüm tutarlılığı | `RidesController`, `AuthController`, `DeviceAuthController`, `TelemetryController`, `WebhooksController` sürümsüz rotalarda (`/api/...`). | Rota değişikliği mevcut istemcileri (simülatörler, mobil) kırar; bir sonraki kırıcı sürümde birlikte yapılmalı. |
| Park yasağı / hizmet bölgesi kuralları | `GeofenceEvaluator` canlı bildirim bölgeleri için kullanılıyor, ancak sürüş bitirirken "hizmet bölgesi dışında / park yasağı bölgesinde bırakılamaz" kuralı yok. | Bölge verisi (poligonlar) henüz tanımlanmadı. |
| FieldOps bağlamı | Batarya düşük ve terk edilmiş araç olayları yalnızca log ile "saha görevi" üretiyor. | `FieldTask` aggregate'i ve saha operatörü akışı yazılmadı. |
| Wallet / Billing | Ödeme bilgisi hâlâ `Ride` içinde; fatura, vergi, farklı ödeme yöntemleri yok. | ADR 0019: gerçek karmaşıklık birikene kadar ayrı context gereksiz. |
| Telemetri kuyruğu | Süreç içi `Channel`: API yeniden başlarsa kuyrukta bekleyen okumalar kaybolabilir; yatay ölçeklemede her instance kendi kuyruğunu işler. | ADR 0012; kalıcı kuyruk ihtiyacı henüz doğmadı. |
| Denetçi (Auditor) rolü | Planlanan aktörlerden biri; henüz hiçbir uç kullanmıyor. | İlgili raporlama özellikleri yazılmadı. |
| Boş migration | `20260926134327_AddPaymentPendingStatus` boş. | Uygulanmış migration'lar silinmez; zararsız. |

## 27.09.2026 teknik incelemesiyle kapatılanlar

- **Sızmış ve kullanımdaki sırlar:** tüm sırlar yenilendi, kaynak koddan çıkarıldı, Options + `ValidateOnStart` ile doğrulanıyor (ADR 0021).
- **Başkasının sürüşünü bitirme (IDOR)** ve **başkasının rezervasyonuyla sürüş başlatma:** sahiplik kontrolleri + veritabanı kısıtları.
- **`VehiclesController.Register` kaydetmiyordu** (bu listede 23. günden beri açıktı): artık kaydediyor ve `201` dönüyor.
- **Terk edilen sürüşte araç sonsuza kadar `InRide` kalıyordu:** sürüş ücretlendirilip kapatılıyor, araç bakıma alınıyor.
- **Retry/DLQ çalışmıyordu, çift tahsilat riski, outbox mesaj kaybı, işlevsiz webhook** (ADR 0013, 0015, 0022, 0023).
- **Cihaz/kullanıcı token ayrımı, global rate limit, kilitleme yokluğu, hata ayrıntısı sızması** (ADR 0021).
- **Application'ın EF Core bağımlılığı** (38. gün notu): `ConcurrencyConflictException` / `UniqueConstraintViolationException`
  soyutlamalarıyla kaldırıldı; mimari test bunu artık denetliyor.
- **Sabit `"default-region"`** (74. gün notu): bölge, aracın konumuna göre hizmet bölgelerinden çözümleniyor.
- **Konum saklama süresi uygulanmamıştı** (ADR 0004): `DataRetentionService` eklendi.
- **Ölçüm testleri her zaman kırmızıydı** (39. gün notu): artık raporluyor ve `Category=Measurement` ile ayrılıyor; N+1 ve
  deadlock gözlemleri gerçek doğrulamalara dönüştü. `dotnet test` yeşil.
- **Entegrasyon testleri geliştirici makinesine bağımlıydı:** sırlar test başına üretiliyor; Redis/RabbitMQ gerekmiyor.
- **Ölü kod:** `TransactionBehavior`, `IEventPublisher`, `PaymentAuthorizedIntegrationEvent`, `Reservation`, `DeviceId`,
  Worker şablonu, `UnitTest1`, `weatherforecast` .http örnekleri kaldırıldı; Kafka deneyi test projesine taşındı.
- **Altyapı:** merkezi paket yönetimi, xunit v3, CI, Dockerfile'lar, health check'ler, sabit imaj sürümleri, yalnızca
  localhost'a açılan portlar, Redis parolası.

## Geçmiş kayıtlar

### Faz 1 sonu
- `CancelReservationCommand` handler'ı yoktu. — **Kapandı:** sürücü iptali (`CancelReservationCommand`) ve sistem
  iptali (`ExpireReservationCommand`) ayrı komutlar olarak yazıldı.
- `Complete` ucu için doğrulayıcı yoktu. — **Kapandı:** `CompleteRideRequestValidator` (sonlu sayı kontrolü dahil).
- `Wallet` ve `Tariff` domain tipleri yazılmamıştı. — **Kısmen kapandı:** `Tariff` eklendi; `Wallet` açık.
- Migration dosyaları `Persistence/Migrations` altında değil, projenin kökünde. — Kozmetik, bilinçli olarak bırakıldı.
- Kapsam raporu (18. gün): genel çizgi kapsamı %61 idi. — Test sayısı 200'ün üzerine çıktı; güncel kapsam raporu
  henüz üretilmedi (`dotnet test --collect "XPlat Code Coverage"` ile alınabilir).

### 23-30. günler
- **23. gün:** `VehiclesController.Register` `SaveChangesAsync` çağırmıyordu. — **Kapandı (27.09.2026).**
- **24. gün:** `Ride` alanları yalnızca `get;` olduğu için ilk migration'a girmemişti; `FixMissingRideColumns` ile düzeltildi.
  Ders: yalnızca `get;` olan alanların gerçekten veritabanına yazıldığını migration dosyasından doğrulamak.
- **26. gün — OWASP taraması:** `DriverId` istek gövdesinden alınıyordu; token'dan okunacak şekilde düzeltildi. Aynı
  taramada `Complete` ucunun sahiplik kontrolü atlanmıştı. — **Kapandı (27.09.2026).**
- **28. gün:** `appsettings.Development.json` dört commit boyunca gerçek (yerel) parola ve anahtarlar içeriyordu. Depo
  herkese açık olduğu ve aynı değerler kullanımda kaldığı için bu, kritik bir açığa dönüşmüştü. — **Kapandı:** tüm
  değerler yenilendi (bkz. açık borçlar: geçmişin yeniden yazılması).
- **30. gün:** Yalnızca `VehiclesController` sürümlendi. — Açık (bkz. "Sürüm tutarlılığı").

### 31-40. günler
- **31. gün — kanıtlanmış yarış durumu:** 50 eşzamanlı rezervasyondan 7'si başarılı oluyordu. — **Kapandı (38. gün):**
  xmin ile iyimser eşzamanlılık; `ParallelRideStartTests` kalıcı regresyon testi.
- **38. gün:** Application, `DbUpdateConcurrencyException` için EF Core'a bağımlıydı. — **Kapandı (27.09.2026).**
- **39. gün:** Ölçüm testleri `throw XunitException` ile rapor veriyordu. — **Kapandı (27.09.2026).**
- **40. gün — Faz 2 kapanışı:** Saha Operatörü ve Denetçi rolleri kullanılmıyordu. — **Kısmen kapandı:** `FieldOperator`
  bakım uçlarında kullanılıyor; Denetçi açık.

### 50-74. günler
- **50/60. gün:** Redis, telemetri hattı ve arka plan servisleri sıralama gereği sonraya kalmıştı. — Tamamlandı.
- **69-70. gün:** DLQ izleme mekanizması yok. — Açık.
- **74. gün:** SignalR bildirimleri sabit `"default-region"` grubuna gidiyordu. — **Kapandı (27.09.2026).**
