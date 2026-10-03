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
- **81. gün:** `AddScootlyApplication()`, tüm handler'ları (Fleet, Riding, Payments, Telemetry) koşulsuz tek pakette kaydediyor — bu yüzden Mvc projesi, henüz ödeme akışını hiç kullanmasa bile `AddScootlyPaymentGateway()`'i çağırmak zorunda kaldı (`ChargeRideCommandHandler`'ın `IPaymentGateway` bağımlılığı `ValidateOnBuild` tarafından zorunlu kılınıyor). İleride `AddScootlyApplication()`, context bazlı ayrı metotlara (`AddFleetHandlers()`, `AddRidingHandlers()`, `AddPaymentHandlers()` gibi) bölünebilir, her sunum katmanı yalnızca gerçekten kullandığını kaydeder.
- **81. gün:** `AddScootlyApplication()`, tüm handler'ları (Fleet, Riding, Payments, Telemetry) koşulsuz tek pakette kaydediyor — Mvc, ödeme akışını hiç kullanmasa bile `AddScootlyPaymentGateway()`'i çağırmak zorunda kaldı. İleride context bazlı ayrı metotlara bölünebilir.
- **84. gün:** `VehiclesController` (Api) içinde artık kullanılmayan `IApplicationDbContext _dbContext` alanı duruyor — `IVehicleReadService`'e taşınan sorgular bu bağımlılığı gereksiz kıldı, temizlenebilir.
- **85. gün:** `_Pagination.cshtml` yalnızca Vehicles listesinde kullanılıyor; ileride başka listelenebilir kaynaklar (FieldTask, ServiceArea) eklendiğinde gerçekten paylaşılan bir bileşen olduğu doğrulanmalı.
- **87. gün:** Mvc tarafı için (cookie tabanlı giriş, rol bazlı 403/AccessDenied) otomatik bir entegrasyon testi yazılmadı — mevcut `ScootlyApiFactory`, `Scootly.Api`'nin `Program` sınıfına ve JWT girişine özel, Mvc'nin cookie akışına uygun değil. Davranış, gerçek tarayıcı ile elle doğrulandı (surucu-b/Driver → /FieldTasks → 403/AccessDenied). İleride ayrı bir `Scootly.Mvc.IntegrationTests` projesi (kendi `WebApplicationFactory<Scootly.Mvc.Program>` fabrikası, cookie container'lı `HttpClient`) açılırsa bu otomatikleştirilebilir.
  - **88. gün:** CORS, Mvc origin'ini sabit olarak `appsettings.Development.json`'a yazıyor — 99. günde Nginx ile aynı origin olunca bu ayar artık gerekmeyecek ama kaldırılmayacak, sadece kullanılmayacak (dokümanın kendi notu).
- **89. gün:** `MapController`, kısa ömürlü JWT'yi her sayfa yüklemesinde yeniden üretiyor — süre dolduğunda (15 dk) sayfa yenilenmeden SignalR bağlantısı otomatik kopacak, kullanıcı sayfayı yenilemek zorunda kalacak. İleride bir "token yenileme" mekanizması eklenebilir.
- **90. gün:** CSP'deki `connect-src`/`img-src` adresleri (`http://localhost:5016`) sabit yazılı — üretim ortamında gerçek Api adresine göre `appsettings`'ten okunacak şekilde parametrik hale getirilmeli.
- **93. gün:** `TraceParent` sütunu eklenmeden önce yazılmış outbox satırlarında alan `null`; onların `publish` span'leri kendi başına yeni bir iz açar. Yeni satırlarda sorun yok, eski satırlar zaten işlenmiş olacağından pratik etkisi yok.
- **93. gün:** Outbox yayını yoklama aralığıyla çalıştığı için Jaeger'da kök istek ile `publish` span'i arasında yaklaşık yarım saniyelik boşluk görünüyor. Bu bir hata değil, outbox deseninin gecikme maliyeti; gecikme hedefi sıkılaşırsa yoklama aralığı gözden geçirilebilir.
- **94. gün:** `scootly.outbox.pending` hem Api hem Worker tarafından raporlanıyor (ikisi de aynı tabloyu sayıyor). Panolarda `sum` değil `max` kullanılmalı, aksi halde değer çift sayılır.
- **94. gün:** Sürüş sayaçları yalnızca Api'de sayılıyor ve süreç içi bellekte tutuluyor; yeniden başlatmada sıfırlanır (Prometheus `rate()` bunu tolere eder).
- **94. gün:** Prometheus verisi için volume tanımlı değil, container yeniden oluşturulunca geçmiş metrikler kaybolur. Yerel geliştirme için kabul edildi.
- **94. gün:** Metrik export aralığı geliştirmede 15 sn'ye çekildi (varsayılan 60 sn); üretimde yapılandırmadan okunmalı.
- **95. gün:** Worker heartbeat yalnızca süreç canlılığını gösterir; tek tek işlerin (ödeme yeniden deneme, terk edilmiş sürüş tespiti) çalıştığını kanıtlamaz. Bir arka plan servisi sessizce takılırsa heartbeat yine yazılır.
- **95. gün:** Compose'taki Worker healthcheck'inin yalnızca "sağlıklı" yönü doğrulandı (`healthy` görüldü). `unhealthy`'ye dönüşü gerçek bir arıza üreterek denenmedi.
- **95. gün:** Mvc ve Api aynı `/health/ready` rotasını raporluyor; Grafana panoları `http_route` ile gruplandığı için ikisi birbirine karışıyor. Servis ayrımı için `service_name` kırılımı eklenebilir.
- **95. gün:** Grafana anonim Viewer erişimi yalnızca yerel geliştirme içindir; ortak/üretim ortamında kapatılmalı. Pano provisioning ile geldiği için `allowUiUpdates: false`, elle yapılan değişiklikler kalıcı değil.
  - **95. gün:** Mvc sağlık uçları ve Worker heartbeat için otomatik test yok; yalnızca elle doğrulandı (Mvc `ready` 503/200, Worker konteyneri `healthy`).
- **95. gün:** Postgres kapalıyken `/health/ready` yanıt vermeden önce birkaç saniye bekliyor gibi görünüyor (p95 panelinde ~4 sn); sağlık kontrolü zaman aşımı kısaltılabilir. Ölçülmedi, panelden çıkarım.
- **96. gün:** Mvc konteynerinde `Seq:ServerUrl` ve `Otel:Endpoint` boş (Production'da `appsettings.Development.json` okunmaz); konteynerdeki Mvc'nin logları Seq'e, izleri Jaeger'a gitmez. Compose'a `Seq__ServerUrl` ve `Otel__Endpoint` (konteynerlerden `http://seq:80`, `http://otel-collector:4317`) eklenmeli.
- **96. gün:** Mvc konteyneri `Production` ortamında cookie `SecurePolicy=Always` kullandığı için `http://localhost:5096` üzerinden giriş çalışmaz; TLS ya da forwarded headers 99. günde (Nginx) çözülecek.
- **96. gün:** Mvc `ApiBaseUrl` ve CSP `connect-src` hâlâ `http://localhost:5016`'ya sabit; konteynerler arası adres tarayıcıdan erişilemez olduğundan şimdilik doğru ama ortama göre parametrik değil.
- **96. gün:** Mvc Compose healthcheck'i `CMD-SHELL` yerine açıkça `bash` çağırır, çünkü imajdaki `sh` dash'tir ve `/dev/tcp` desteklemez. Bu yöntem `bash`, `head`, `grep` gibi imaj araçlarına bağlıdır; temel imaj değişirse healthcheck sessizce `unhealthy`'ye dönebilir.
- **96. gün:** Mvc healthcheck'inin hatalı durumda `unhealthy` verdiği doğrulanmadı; yalnızca `healthy` yönü Compose üzerinden görüldü. `kill -STOP 1` ile dondurma denendi ancak PID 1 sinyali yok saydı (`/proc/1/status` `S (sleeping)` kaldı), test geçersiz. Çıkış kodu testi de PowerShell tırnak işleme sorunu nedeniyle ölçülemedi.