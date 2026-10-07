# Teknik Borç Listesi

Bu dosya, bilinçli olarak şimdi düzeltilmeyen ama fark edilen eksiklikleri kaydeder. Kapatılan maddeler silinmez;
nasıl kapatıldığıyla birlikte aşağıdaki geçmiş bölümünde tutulur.

## Gun 109: son durum (6 Ekim 2026)

### Kapatilan ya da ilerleyenler

- FieldOps baglami: kapandi. Saha gorevi artik `FieldTask` aggregate'i ile yurutuluyor (ADR 0025, fotograf icin ADR 0043). Eski "Acik borclar" satiri kaldirildi.
- RabbitMQ yeniden baglanma hatasi: kapandi (106. gun maddesine bakin; olculenler ve olculmeyenler orada).
- Secret taramasi: KISMEN. GitHub Secret Protection ve Push protection acik (ekran goruntusunde "Disable" dugmesi gorunuyor); sizinti denemesiyle sinanmadi. CI'da gitleaks yok.
- Domain kapsami: coverlet ile yalnizca `Scootly.Domain` ve yalnizca `Scootly.Domain.UnitTests` icin olculdu: satir %78,8 -> %90,9, dal %73,7 -> %91,7 (hedef >= %80). CI birlesik raporu bu gun yeniden okunmadi.

### Bilincli birakildi

| Konu | Gerekce |
|---|---|
| Git gecmisindeki eski sirlar | Hepsi 27.09.2026'da yenilendi; gecmisi yeniden yazmak paylasilan dallari bozar (ADR 0021). |
| Arac basina cihaz kimligi | Gercek cihaz filosu yok; provizyon ve sir dagitimi gerektirir (ADR 0021). |
| Rota surumleme (`/api/...`) | Mevcut istemcileri kirar; bir sonraki kirici surumde birlikte yapilacak. |
| Park yasagi / hizmet bolgesi kurali | Bolge verisi (poligonlar) tanimli degil. |
| Wallet / Billing | Gercek karmasiklik birikene kadar ayri context gereksiz (ADR 0019). |
| Surec ici telemetri kuyrugu | Kalici kuyruk ihtiyaci dogmadi (ADR 0012). |
| Denetci (Auditor) rolu | Ilgili raporlama ozellikleri yazilmadi. |
| Bos migration | Uygulanmis migration silinmez; zararsiz. |

"DLQ izleme / alarm" bilincli birakildi DEGIL, acik kaliyor: eski gerekce ("gozlemlenebilirlik altyapisi yok") artik gecerli degil; alarm kurali ve DLQ'dan yeniden surme mekanizmasi yok.

### Acik kalanlar (Gun 110 risk listesine girecek)

- DLQ izleme / alarm kurallari ve DLQ'dan yeniden surme yok.
- Redis kapaliyken (yeniden) baslayan Mvc replikasi 500 veriyor.
- Docker healthcheck yalnizca `/health/live`; bagimlilik kesintisinde konteyner `healthy` kaliyor.
- Ayni `RideCompleted` mesajinin tekrar tesliminde retry hakki tukeniyor.
- Prod'da gozlemlenebilirlik (Seq, Jaeger, Prometheus, Grafana) yok.
- 108b nesne deposu prod'a bagli degil; en az yetkili kimlik ve icerik taramasi yok.
- Mvc yerel (`dotnet run`) sirlari README'de belgelenmedi.

## Açık borçlar

| Konu | Açıklama | Neden ertelendi |
|---|---|---|
| Git geçmişinde eski sırlar | 28. gün öncesi commit'lerde eski JWT anahtarı, cihaz sırrı ve DB parolası duruyor. Hepsi 27.09.2026'da yenilendi, artık geçersiz. | Geçmişi yeniden yazmak (`git filter-repo`) paylaşılan dalları bozar; rotasyon sızıntının etkisini zaten ortadan kaldırdı (ADR 0021). |
| Araç başına cihaz kimliği | Cihazlar ağ geçidi modeliyle (tek istemci, çok araç) doğrulanıyor. | Provizyon ve sır dağıtımı gerektirir; gerçek cihaz filosuna geçişte değerlendirilecek (ADR 0021). |
| DLQ izleme / alarm | Ölü mektup kuyruklarına düşen mesajlar yalnızca RabbitMQ yönetim arayüzünden görülebiliyor. | Gözlemlenebilirlik altyapısı (metrik, alarm) henüz yok. |
| Sürüm tutarlılığı | `RidesController`, `AuthController`, `DeviceAuthController`, `TelemetryController`, `WebhooksController` sürümsüz rotalarda (`/api/...`). | Rota değişikliği mevcut istemcileri (simülatörler, mobil) kırar; bir sonraki kırıcı sürümde birlikte yapılmalı. |
| Park yasağı / hizmet bölgesi kuralları | `GeofenceEvaluator` canlı bildirim bölgeleri için kullanılıyor, ancak sürüş bitirirken "hizmet bölgesi dışında / park yasağı bölgesinde bırakılamaz" kuralı yok. | Bölge verisi (poligonlar) henüz tanımlanmadı. |
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
- **96. gün:** Mvc healthcheck mantığı betikle iki yönde doğrulandı (8080 → çıkış 0, 9999 → çıkış 2); Docker'ın bu çıkış kodunu `unhealthy` olarak işaretlemesi ise gerçek bir arıza üretilerek denenmedi (`kill -STOP 1` PID 1'de işlemedi).
   - **96. gün:** Mvc healthcheck mantığı betikle iki yönde doğrulandı (8080 → çıkış 0, 9999 → çıkış 2) ve Docker'ın sıfırdan farklı çıkış kodunu `unhealthy` saydığı boş bir aspnet konteynerinde (`--health-cmd "exit 1"`) görüldü. Mvc konteyneri üzerinde gerçek bir arıza üretilerek doğrudan denenmedi (`kill -STOP 1` PID 1'de işlemedi).
   - **97. gün:** Graceful shutdown deneyinde `docker stop` 0,46 sn'de ve `ExitCode=0` ile tamamlandı, ancak kapanış anında işlenmekte olan bir mesaj bulunmadığı için tüketicinin `requeue` yolu gözlenmedi. Yeni eklenen kapanış log satırı henüz hiç tetiklenmedi.
  - **97. gün:** Graceful shutdown deneyi (ödeme simülatörü dondurulmuş): `docker stop` 0,46 sn, `ExitCode=0`; tüketici işlenmekte olan `RideCompleted` mesajını `requeue` ile bıraktı ve yeni kapanış log satırı tetiklendi. Konteyner yeniden başlayınca ödeme alındı. Loglanan ödeme kimliğinin mesaj kimliğiyle eşleştiği doğrulanmadı.
- **97. gün:** Kapanışta görülen `ERR` (Polly zaman aşımı) ve uzun yığın izi, deney düzeneğinden (simülatörün dondurulması) kaynaklanıyor; boşta kapanış temiz (`Application is shutting down...` dışında hiçbir satır yok).
- **97. gün:** Konteynerde ASP.NET Core DataProtection anahtarları kalıcı değil ve şifrelenmiyor (`/home/app/.aspnet/DataProtection-Keys`). Konteyner yeniden oluşturulunca Mvc'nin cookie ve antiforgery korumaları geçersiz olur; birden fazla Mvc kopyasında anahtar paylaşılmadığı için oturumlar tutarsız çalışır. Anahtarlar Redis'e ya da bir volume'a taşınmalı, 98. günde üretim yapılandırmasıyla birlikte ele alınabilir.
- **98. gün:** Üretim Compose'undaki kaynak sınırları boşta ölçümle (kullanım sınırın %20'sinin altında) ve ilk tahminle belirlendi; yük altında ölçülmedi.
- **98. gün:** `restart: unless-stopped` yapılandırmada görüldü ancak süreç çökmesinde gerçekten yeniden başladığı denenmedi; log rotasyonu da (10 MB x 3) tetiklenmedi.
- **98. gün:** Temel dosyadaki sabit `container_name` yüzünden geliştirme ve üretim yığınları aynı anda çalıştırılamaz.
- **98. gün:** `.env.prod` temel `.env` değişkenlerinin tümünü içermek zorunda (`--env-file` varsayılan `.env`'i okumaz); Grafana parolası üretimde kullanılmasa da gerekli.
- **99. gün:** `ip_hash` ile `/hubs/` dağılımı Docker Desktop'ta tek kaynak IP olduğu için gösterilemedi; iki Api kopyası arasında canlı araç güncellemesi de (SignalR) sınanmadı.
- **99. gün:** Hız sınırlayıcı kopya başına bellekte; Api 2 kopyada iken sınır kopyalar arasında bölünür. Dağıtık sayaç gerekir.
- **99. gün:** Nginx upstream adreslerini açılışta çözer; bir Api kopyası yeniden başlayıp IP'si değişirse Nginx yeniden başlatılmalı ya da `resolver` ile dinamik çözümleme kurulmalı.
- **99. gün:** Kendinden imzalı sertifika tarayıcı uyarısı verir; üretimde gerçek bir otorite (ör. Let's Encrypt) ve yenileme otomasyonu gerekir.
- **99. gün:** İlk dağıtımda iki Api kopyası aynı anda `IdentityBootstrapper`'ı çalıştırdığı için biri `UserNameIndex` benzersizlik hatası alıp `ERR` logluyor (uygulama çökmüyor, `restarts=0`); yarış zararsız ama gürültülü.
- **99. gün:** Üretimde Api loglarında `access_token` sızıntısı olmadığı ölçülmedi; yalnızca `Microsoft.AspNetCore` günlüğünün Information olmaması nedeniyle yazılmadığı düşünülüyor.
- **99. gün:** `X-Forwarded-*` güveninin (`KnownProxies`) çalıştığı ve Mvc'nin `UseForwardedHeaders` değişikliği geliştirme ortamında (CSP `connect-src` yeni üretimle, `http://localhost:5016`) denenmedi.
- **100. gün:** Kayıt ucu dışındaki hız sınırı politikalarının değerleri okunmadı; 103. günde (k6) giriş ucunun IP başına ve kullanıcı başına yazma sınırlarının yükü nasıl etkilediği önce dosyadan doğrulanmalı.
- **100. gün:** Konsol şablonu (`{Properties:j}`) yalnızca Api'de doğrulandı; Mvc ve Worker için yeniden derleyip korelasyon aramasıyla denenmedi. Mvc'nin istek özetinde CorrelationId alanı olup olmadığı bilinmiyor.
- **100. gün:** `{Properties:j}` log satırlarını uzatıyor; JSON biçimi (Serilog.Formatting.Compact) daha temiz bir alternatif, karar verilmedi.
- **100. gün:** 500 araçlık yükte tek bir turda iki parti arası 8 sn geçti (diğerlerinde 5 sn); nedeni bulunmadı. Yük testi (103. gün) bu sapmanın tekrarlanıp tekrarlanmadığına bakmalı.
- **100. gün:** Tek makineden k6 çalıştırılınca tüm istekler tek IP görünür (Nginx logunda `10.231.0.1`); `Auth` (10/dk, IP başına) ve `Anonymous` (60/dk) sınırları sanal kullanıcıları ortak sayar. k6 girişi `setup()` aşamasında bir kez yapmalı ve `User` politikası için her sanal kullanıcı kendi hesabını kullanmalı; ya da `RateLimiting__*` ortam değişkenleri geçici yükseltilmeli. Doğrudan `localhost:5016`'da da aynı olduğu ölçülmedi. Kayıt betiği 429'daki `Retry-After` başlığını okumuyor.
- **101. gün:** Worker kapsamı %0 (tüm iş sınıfları) ve Mvc ölçülmüyor (test projesi yok). Kapsamı %0 olanlar: `FleetHub`, `SignalRFleetNotifier`, `RideChargeConsumer`, `VehicleStatusNotificationConsumer`, `OutboxPublisherService`, `OutboxMetricsCollector`, `RedisCacheService`, `RabbitMqHealthCheck`, `UpdateVehicleDetailsCommand` ve handler'ı. 104. gün Playwright testi düzenleme akışını kapsayacak.
- **101. gün:** `MessagingTests.Basarili_Mesaj_Onaylanmali_Ve_DLQya_Dusmemeli` CI'da bir kez zaman aşımına düştü; düzeltme (tüketici kaydını bekleme) yarışın kapandığını kanıtlamıyor. Tekrar düşerse hipotez yanlıştır, `StartConsumerAsync`'ten bağımsız bir kök neden aranmalı.
- **101. gün:** Actions Node 20 uyarısı (`cache`, `checkout`, `setup-dotnet`, `upload-artifact`): hangi yeni ana sürümler olduğu kontrol edilmeden yükseltme yapılmadı.
- **101. gün:** Runner `ubuntu-24.04`'e sabit; `ubuntu-latest` 19 Ekim 2026'dan sonra Ubuntu 26 olacak. Sabit etiketi güncellemek ayrı, bilinçli bir karar olmalı (Docker/Testcontainers davranışı değişebilir).
- **101. gün:** NuGet önbelleği (~361 MB, dal başına) süre kazandırmadı (ölçüm gürültülü); birkaç çalıştırma sonra faydası yeniden değerlendirilmeli.
- **102. gün:** `release.yml` yalnızca `actionlint` ile denetlendi, gerçek bir yayın yok; `gh run list --commit` bayrağı, GHCR'a itme ve GitHub Release oluşturma ilk etiketle sınanacak. CHANGELOG bölümü boş satırlardan oluşsa da `-s` kontrolünü geçer (yalnızca başlık bulunması denetleniyor).
- **102. gün:** Yedek yalnızca ayrı bir veritabanına geri yüklendi; asıl veritabanının üzerine yükleme, uygulamanın geri yüklenen veritabanıyla açılması ve `Down` migration'ı hiç çalıştırılmadı (runbook bölüm 7).
- **102. gün:** Compose `image:` adları yerel (`scootly-api:${SCOOTLY_VERSION:-1.0.0}`); GHCR'dan çekme yolu için `image:` satırları ayrıca ele alınmalı. Aynı sürüm etiketiyle yeniden derleme yerel imajın üstüne yazar.
- **102. gün:** `v0.1.0-rc.1` yayınlandı ama yalnızca `scootly-api` imajı çekilip incelendi; diğer dört imajın içeriği ve yayınlanan imajlarla uygulamanın bir veritabanıyla açılması denenmedi. Hatalı yollar (kırmızı CI ile etiket, olmayan CHANGELOG bölümü) GitHub'da sınanmadı.
- **102. gün:** Beş GHCR paketi herkese açık görünüyor (bos Docker yapılandırmasıyla erişildi) ama nedeni bilinmiyor ve görünürlük ayarı elle değiştirilmedi; imajlardaki ayar dosyaları yalnızca anahtar adına göre tarandı, kapsamlı bir sır taraması yapılmadı. GHCR'da sürüm saklama/silme politikası belirlenmedi.
- **103. gun:** Odeme tuketicisi yuk altinda 5,44 odeme/sn ile birikme yaratti (100 kullanici/60 sn sonrasi 2647 surus `Pending`, sonunda hepsi `Paid`). Hipotez: tuketici eszamanliligi varsayilan (1) ve simulator gecikmesi (50-300 ms); dogrulama deneyi yapilmadi. Uretimde 2 Api kopyasi ayni kuyrugu paylasir (olculmedi).
- **103. gun:** Telemetri tek tuketicisi sicak yiginda yaklasik 24 parti/sn boşaltiyor; kuyruk bellek ici ve 10.000 okumalik (202 = kuyruga alindi). Surec cokerse tamponda bekleyen okumalar kaybolur (koddan; denenmedi).
- **103. gun:** 503 geri basinc yanitlari Api istek gunlugunde `ERR` seviyesinde (120 dk'da 371 satir): beklenen davranis alarm gurultusu uretiyor.
- **103. gun:** Yuk testleri sinirsiz gelistirme yiginda yapildi; uretim override'indaki Api siniri (1,0 CPU, 512 MiB) altinda tekrarlanmadi ve Postgres 1 GiB siniri dogrulanmadi (gelistirmede 4,8 milyon satirda 760 MiB goruldu). Nginx, 2 kopya ve acik hiz sinirlayici ile yuk testi yok.
- **103. gun:** Yuk testi sirasinda (13:41 yerel) yigin topluca yeniden basladi; neden bilinmiyor. Yeniden baslatmadan hemen sonra tek tuketici hizi bir kosuda daha dusuk goruldu (20,7 parti/sn); soguk baslangic hipotezi tek tekrarla desteklendi.
- **103. gun:** `TelemetryReadings` tablosu buyudukce tuketici hizi degisiyor mu bilinmiyor; Worker'daki `DataRetentionService`'in telemetriyi temizleyip temizlemedigi okunmadi.
- **103. gun:** Yuk testi verisi gelistirme veritabaninda birakildi: yaklasik 3930 surus, yaklasik 103 kullanici (3 asil + `lt-user-1..100`, parola kaynakta `LoadTest1234`) ve yaklasik 5,28 milyon telemetri satiri (hesap; 3.810.157 sayimi + sonraki kosu farklari). Yedekten asil veritabaninin uzerine geri yukleme denenmedi.
- **104. gun:** E2E testleri kod kapsami raporuna girmez: Mvc ayri surecte kostugu icin coverlet onu buyuk olasilikla olcmez (tasarimdan; denenmedi) ve `e2e` isi kapsam adimina dahil degil. 101. gundeki `UpdateVehicleDetailsCommand` %0 satiri raporda degismeyecek, ama akis artik bir E2E testiyle kosuluyor.
- **104. gun:** E2E `Development` + http ile kosar; Secure cookie, HSTS ve Nginx TLS yolu hicbir otomatik testle kapsanmiyor (99. gunde elle gorulmustu).
- **104. gun:** E2E kapsami iki senaryo: arac duzenleme ve gorev ustlenme. Harita sayfasi, arac olusturma, gorev tamamlama, dogrulama hatasi ve yetkisiz erisim (AccessDenied) yok.
- **104. gun:** E2E kararsizligi bilinmiyor; yerelde ve CI'da yalnizca birkac kez kosuldu. Kirmizi bir kosuda artifact'a (ekran goruntusu, HTML, Mvc logu) bakilmali; kararsiz oldugu gorulurse tekrar sayisi artirilip nedeni aranmali.
- **104. gun:** CI'da her kosuda Chromium (yaklasik 187 MiB) ve headless shell (yaklasik 114 MiB) indirilip `apt` ile bagimliliklar kuruluyor; tarayici onbellegi denenmedi, kazanc olculmedi. `e2e` isinin toplam suresi kaydedilmedi.
- **104. gun:** Fixture Mvc'yi `bin/{Debug|Release}` altindan `#if DEBUG` ile secer ve bos portu dinleyiciyi kapatarak bulur (yaris penceresi); Mvc `Development` ortaminda acildigi icin gelistirici makinesindeki user-secrets okunabilir (ortam degiskenleri ayni anahtarlari ezer). CI'da bu sorun yok; yerelde bir farklilik gorulurse ilk bakilacak yer burasi.
- **105. gun:** Redis kalici degil (AOF veya hacim yok) ve DataProtection anahtar halkasi (`Scootly:Mvc:DataProtection-Keys`) sifrelenmeden orada duruyor. Redis sifirlanirsa acik oturumlar ve antiforgery belirtecleri gecersiz olur (beklenen, olculmedi); anahtar sifreleme (`ProtectKeysWith...`) eklenmedi.
- **105. gun:** Cok kopyali calismayi dogrulayan otomatik test yok; uc kusur (IP cakismasi, `zone`, DataProtection) elle olculerek bulundu. Ayni sinif bir kusurun geri gelmesini hicbir test yakalamaz.
- **105. gun:** `docker-compose.green.yml` prod override'indaki ortam degiskenlerini ve kaynak limitlerini elle kopyalar; biri degisirse digeri guncellenmezse green farkli ayarla acilir.
- **105. gun:** Green'in `depends_on` degerlerini `extends` ile miras aldigi `docker compose config` ile gorundu (onceki "tasimaz" varsayimi yanlisti); altyapi kapaliyken `up` davranisi denenmedi.
- **105. gun:** Canli renk `active.conf`'un icerigidir ve elle degistirilir; dosya git'te izlenir ama gecisi yapan kisi commit'i atmazsa depo ile calisan durum ayrisir. Otomasyon ve denetim yok.
- **105. gun:** Gelistirme ve prod yiginlari sabit `container_name` yuzunden ayni anda calismaz; prod'u acmadan once gelistirme yigini (`deploy` projesi) durduruldu (`-v` kullanilmadi, hacimler duruyor) ve geri baslatilmasi gerekiyor.
- **105. gun:** Nginx upstream adlarini baslangicta ve reload'da cozer; kopyalar yeniden olusturulunca reload gerekir. Etkin `active.conf` green'i gosterirken green durdurulursa reload'un davranisi denenmedi.
- **105. gun:** Blue api konteynerinin imaj kimligi (`sha256:0a78...`) `scootly-api:1.0.0` etiketinin kimligiyle (`ea4a6dd7...`) eslesmiyor (Docker Desktop containerd deposu; tam kimlikle `docker image inspect` "No such image"); neden belirlenmedi. `Scootly.Api.dll` SHA-256'si calisan konteynerde, 1.0.0'da ve 1.0.1'de ayni; yalnizca bu dosya karsilastirildi. Provada api 1.0.1 ile 1.0.0 ayni koddu, yani yeni api kodu denenmedi.
- **105. gun:** Giris (kimlik) cookie'sinin blue-green gecisinden sonra gecerliligi, uzun omurlu baglantilar (SignalR/WebSocket), yazma istekleri ve Worker'in surum gecisi olculmedi (antiforgery cookie+token ve kisa GET istekleri olculdu, 8.7).
- **105. gun:** Depoda `.gitattributes` yok; `deploy/` altindaki yeni dosyalar icin Git "LF will be replaced by CRLF" uyarisi verdi. Windows'ta CRLF'ye donen yapilandirma dosyalarinin Linux konteynerinde (nginx, compose) soruna yol acip acmadigi olculmedi (calisma kopyasi LF iken nginx kabul etti).
- **105. gun:** Kapsam raporu (CI, 1ab438d): satir %73,7, dal %60,6. Worker %0, `FleetHub` ve `SignalRFleetNotifier` %0, `RideChargeConsumer`/`VehicleStatusNotificationConsumer` %0, `RedisCacheService` %0, `OutboxPublisherService` %0, `GlobalExceptionHandler` %12,5, `ServiceAreaRegionResolver` %4,3. `UpdateVehicleDetailsCommand(Handler)`, `FieldTaskRepository` ve `FieldTaskReadService` %0 gorunuyor; E2E bunlara dokunur ama kapsama girmez (bkz. 104. gun).
- **105. gun:** Ortam farklari tablosu (`deployment.md` bolum 9) koddan ve dosyalardan okundu; calisan bir Production yiginda Swagger'in kapali oldugu, HSTS basligi ve Secure cookie istekle dogrulanmadi.
- **105. gun:** Gelistirme yiginindaki 13:41 toplu yeniden baslamanin nedeni belirlenemedi (Docker olaylari geriye donuk tutulmuyor, o an kayit alinmadi).
- **105. gun:** Calisan prod yiginda Api `/swagger/index.html` anonim istekte 401 dondu (404 degil); neden bakilmadi (ornegin genel yetkilendirme politikasi). HSTS basligi `localhost` icin varsayilan olarak gonderilmedigi icin istekle dogrulanamadi.
- **105. gun:** Ayni `RideCompleted` mesajinin cift teslimi reddedilmis bir odemede 5 deneme hakkindan 2'sini tuketiyor (olculdu: 30 surus, `Pending|2|30`, 60 ret satiri); onay yolunda zararsiz (30/30 `Paid`, deneme 1). `RideChargeConsumer` `MessageId`'yi yalnizca gunluge yazar, tekrar kontrolu yapmaz (koddan okundu); deney mesajlarinda MessageId yoktu. Gercek yayinlarda (outbox) cift teslimin ne siklikla oldugu olculmedi; cozum (tuketici tekrar kontrolu veya deneme sayacini mesaja gore sinirlama) secilmedi.
- **105. gun:** Odeme tuketicisi deneylerinde (A ve B) eszamanli cift teslimde yarisin (iki kopyanin ayni anda ayni surusu okumasi) gercekten yasandigi dogrulanamadi: sonuc tutarli ama yarisin kendisi gozlenmedi. Simulator Production'da istek gunlugu tutmadigi icin saglayiciya giden istek sayisi da olculmedi.
- **105. gun:** Odeme simulatorunun `/api/failure-rate` ucu yalnizca `Development`'ta var; prod yiginda ret/kesinti deneyi icin simulatoru gecici bir override (`ASPNETCORE_ENVIRONMENT=Development`) ile yeniden olusturmak gerekti. 106. gun plani da bu ucu kullaniyor.
- **106. gun:** RabbitMQ kesintisinden sonra tuketiciler her kosulda yeniden baglanmiyor: kesintide outbox satiri varken ya da `/health/ready` cagrildiginda 240 sn icinde 0 tuketici kaldi (`ride-charges`, `battery-low`, `VehicleStatusChanged` abonelikleri); hic dokunulmadiginda 13 sn'de dondu. Gecici cozum: Worker ve Api restart + nginx reload. Hipotez (dogrudan gozlenmedi): `RabbitMqConnectionProvider.GetConnectionAsync` kurtarilan baglantiyi dispose ediyor, `RabbitMqConsumerService` eski baglantida kaliyor. **Gun 109: KAPANDI.** Hipotez testle dogrulandi ve duzeltildi (`RabbitMqConnectionProvider`: kurtarilmakta olan baglanti dispose edilmiyor; `RabbitMqRecoveryTests`). Olculen: duzeltme oncesi kesintide saglayiciya dokunulan senaryo kirmizi (60 sn icinde tuketici mesaj almadi), dokunulmayan kontrol senaryosu yesil; duzeltme sonrasi ikisi de yesil. Olculmedi: Compose/Nginx uzerindeki 106. gun provasi tekrarlanmadi; `RabbitMqHealthCheck`in yeni `InvalidOperationException` karsisindaki davranisi okunmadi; kesintide `/health/ready` davranisi olculmedi.
- **106. gun:** Docker healthcheck'i `/health/live` kullaniyor; Postgres, RabbitMQ ya da Redis kesintisinde konteynerler `healthy` kaliyor. Bagimlilik durumunu izleyen bir sey yok (readiness yalnizca elle sorgulaniyor); ayrica `/health/ready` cagrisi RabbitMQ kesintisinde baglantiya dokunuyor (yukaridaki madde).
- **106. gun:** Redis kapaliyken yeniden baslatilan ya da yeni acilan Mvc replikasi 500 veriyor (DataProtection key ring Redis'ten okunamiyor); mevcut replikalar calismaya devam etti. Redis donunce 7 sn'de toparlandi. Kesintide rolling deploy yapilmamali. Nginx'in 500 donen replikaya trafik yollayip yollamadigi olculmedi.
- **106. gun:** Odeme gateway kesintisinde taze surusler retry kuyrugundan ~31 sn'de DLQ'ya dusuyor ve DLQ'dan yeniden suren bir mekanizma yok; satirlar uzlastirma esigine kadar `Pending` kaliyor. Uzlastirma servisi eski `Pending` satirlari gateway donunce tahsil etti (<=30 sn). Esik degerleri (`neverAttemptedBefore`, `retryBefore`) ve DLQ mesajlarinin akibeti olculmedi; DLQ izleme maddesiyle iliskili.
- **106. gun:** Redis kesintisinde giris yapmis Mvc oturumunun ve anahtar kaybinda mevcut cerezlerin davranisi olculmedi (plandaki "DataProtection Redis'teyse cerez cozulemez" uyarisi). `DBSIZE` Redis restart sonrasi 1 kaldi; anahtarin ayni oldugu dogrulanmadi.
- **106. gun:** RabbitMQ kesintisinde Mvc `/health/ready` bir kosuda 503, digerinde 200 dondu; nedeni belirlenemedi. Mvc `/health/ready` basliksiz 400 donuyor (`Host: localhost` gerekiyor); nedeni koddan dogrulanmadi.
- **106. gun:** Outbox yayincisinin log'daki bekleme degeri RabbitMQ kesintisinde sabit 2 sn goruldu (kodda 60 sn'ye kadar buyumesi bekleniyor); nedeni olculmedi.
- **106. gun:** Ariza provalari yerel Docker Desktop'ta tek kosuyla yapildi; `docker stop` DNS kaydini sildigi icin Postgres/RabbitMQ arizalari "Name or service not known" olarak gorundu. Takili ya da baglantiyi reddeden sunucu ve ag bolunmesi taklit edilmedi. Ayrintilar: `docs/runbook/failure-drills.md`, `docs/adr/0042-kasitli-ariza-provasi.md`.
- **107. gun:** CI'da secret taramasi yok (GitHub secret scanning / push protection ya da gitleaks); yalnizca paket zafiyet taramasi var (`ci.yml` "Check vulnerable packages"). 27.09.2026 postmortem'inin acik aksiyonu.
- **107. gun:** Prometheus'ta alarm kurali yok: `deploy/prometheus/` altinda `alert:`, `groups:`, `rule_files` eslesmesi bulunmadi. Olaylar elle fark ediliyor; DLQ izleme maddesiyle iliskili.
- **107. gun:** Seq, Jaeger, OpenTelemetry Collector, Prometheus ve Grafana yalnizca `observability` profilinde; 106. gunde calisan prod yiginda bu servisler yoktu. Prod'da metrik/iz/log toplama kurulumu yapilmadi.
- **107. gun:** `docs/runbook/incident-response.md` surec bolumleri (roller, onem seviyeleri, hedef sureler, iletisim kanali, olay notu) oneridir; hic tatbikat yapilmadi ve iletisim kanali tanimli degil.
- **107. gun:** Sir rotasyon adimlari (PostgreSQL, RabbitMQ, Redis parola degisikligi, `--force-recreate`) 27.09.2026'da kayda gecmedi; `incident-response.md` bolum 6 denenmedi.
- **107. gun:** Sizan sirlarin her birinin Git gecmisinde ilk gectigi commit belirlenmedi; sizinti suresi yalnizca ust sinirla biliniyor (en fazla 38 gun, 2026-08-20 ile 2026-09-27). Kotu kullanim aramasinin yontemi ve kapsami kayitli degil, "iz bulunmadi" kanitlanmis bir "kullanilmadi" degildir.
- **107. gun:** Postmortem'de "depo ilk bastan beri public" ve "kotu kullanim izi yok" ifadeleri proje sahibinin beyanidir, bagimsiz olarak dogrulanmadi.

## Gun 108b: nesne depolama (fotograf yukleme)

- Nesne deposu prod'a baglanmadi: nginx `client_max_body_size` yok (1 MB, buyuk foto 413), `storage` profili prod'da baslamiyor, on-imzali URL ana makinesi dis dunyadan erisilemez. Durum: acik, prod'a baglanirken yapilacak.
- Uygulama kimligi bucket olusturabiliyor (tembel olusturma). En az yetkili kimlik ve onceden hazirlanmis bucket gerekli. Durum: acik.
- SaveChanges sonrasi hata ve silme hatasi birlikte olursa yetim nesne kalabilir; temizleyici is yok. Durum: bilincli birakildi (olasilik dusuk, etkisi depolama maliyeti).
- Fotograf icin goruntu cozumleme, kotu amacli yazilim taramasi ve EXIF (konum) temizleme yok. Durum: acik, prod'dan once degerlendirilmeli.
- 6 MB'i asan isteklerin tarayicida gorunumu olculmedi. Durum: olculmedi.
- Gelistirmede uygulama cerezi ayni makinede farkli portta calisan depoya gidebilir (cerezler port bazli yalitilmaz). Etkisi olculmedi. Durum: olculmedi.