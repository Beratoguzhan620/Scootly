# Scootly

**Şehir içi elektrikli scooter paylaşım platformunun backend'i ve web arayüzü.** Clean Architecture prensipleriyle, .NET 10 ile geliştirilmiştir. Bu proje bir öğrenme/portföy çalışmasıdır.

Sürüm: `v0.1.0-rc.1` (ilk sürüm adayı, 2026-10-06; ayrıntılar `CHANGELOG.md` içinde).

---

## İçindekiler

- [Genel Bakış](#genel-bakış)
- [Mimari](#mimari)
- [Teknoloji Yığını](#teknoloji-yığını)
- [Proje Yapısı](#proje-yapısı)
- [Servisler, Portlar ve Profiller](#servisler-portlar-ve-profiller)
- [Kurulum](#kurulum)
- [Çalıştırma](#çalıştırma)
- [Saha Görevi Fotoğrafı (Nesne Depolama)](#saha-görevi-fotoğrafı-nesne-depolama)
- [Test ve Kapsam](#test-ve-kapsam)
- [API Uçları ve Web Arayüzü](#api-uçları-ve-web-arayüzü)
- [Güvenlik Notları](#güvenlik-notları)
- [Dağıtım ve İşletme](#dağıtım-ve-işletme)
- [Mimari Kararlar](#mimari-kararlar)
- [Teknik Borç](#teknik-borç)

---

## Genel Bakış

Scootly, kullanıcıların yakınlarındaki elektrikli scooter'ları görüp kiralayabildiği, sürüş başlatıp bitirebildiği; saha ekiplerinin de araç bakımını yürüttüğü bir micro-mobility platformudur:

- **Kimlik ve yetki** — JWT ile giriş, rol tabanlı (Driver, FleetManager, FieldOperator) ve kaynak sahipliği bazlı yetkilendirme; cihazlar için ayrı istemci kimlik bilgisi akışı; hesap kilitleme ve istemci başına rate limiting.
- **Araç ve sürüş yönetimi** — durum makinesiyle korunan araç yaşam döngüsü; rezervasyon sahipliği; sürücü başına tek aktif rezervasyon ve tek aktif sürüş (uygulama + veritabanı kısıtları).
- **Eşzamanlılık güvenliği** — PostgreSQL `xmin` ile iyimser kilitleme; çakışmalarda taze veriyle otomatik yeniden deneme.
- **Ödeme saga'sı** — ücret domain'de tarifeyle hesaplanır; tahsilat outbox → RabbitMQ → ödeme sağlayıcısı akışıyla asenkron yapılır; idempotency anahtarı, imzalı webhook ve uzlaştırma işiyle çift tahsilat ve kayıp önlenir.
- **Güvenilir mesajlaşma** — domain olayları aynı transaction'da outbox'a yazılır; publisher confirm, gecikmeli retry kuyruğu ve ölü mektup kuyruğu (DLQ). RabbitMQ bağlantısı kesilince tüketiciler kendiliğinden toparlanır (bkz. [Test ve Kapsam](#test-ve-kapsam)).
- **Telemetri** — cihazlardan toplu konum/batarya verisi; aracın son bilinen durumu güncellenir, batarya eşiği aşılınca saha görevi üretilir.
- **Saha operasyonu** — saha operatörü açık görevleri üstlenir ve tamamlar; tamamlarken isteğe bağlı fotoğraf (en fazla 5 MB, JPEG/PNG) yüklenebilir.
- **Web arayüzü (Mvc)** — cookie tabanlı giriş, rol bazlı erişim (yetkisiz kullanıcı AccessDenied görür), araç listesi ve düzenleme, canlı harita, sürüşler, panel ve saha görevleri. Playwright ile tarayıcı testleri vardır.
- **Canlı bildirimler** — SignalR ile, hizmet bölgesine göre gruplanmış araç durumu değişiklikleri.
- **Arka plan işleri** — süresi dolan rezervasyonlar, terk edilmiş sürüşler, bekleyen ödemelerin yeniden denenmesi, veri saklama politikası (KVKK).
- **Gözlemlenebilirlik** — Serilog ile yapılandırılmış log (Seq), OpenTelemetry ile dağıtık izleme (Jaeger), Prometheus metrikleri ve Grafana panosu, `/health/live` ve `/health/ready` uçları.
- **İşletme** — Nginx ters vekil (TLS), üretim Compose yapılandırması, elle mavi-yeşil dağıtım, `v*` etiketiyle tetiklenen yayın hattı.

---

## Mimari

Proje **Clean Architecture** (katmanlı mimari) prensiplerine göre kurulmuştur. Bağımlılık oku her zaman içe, Domain'e doğrudur ve mimari testlerle denetlenir:

```
Scootly.Api / Scootly.Mvc / Scootly.Worker
        |
Scootly.Infrastructure
        |
Scootly.Application
        |
Scootly.Domain   (hiçbir dış pakete bağımlı değildir)
```

- **Scootly.Domain** — iş kuralları, durum makineleri, değer nesneleri, tarife, domain olayları.
- **Scootly.Application** — use case'ler (komut/handler), soyutlamalar (repository, saat, ödeme sağlayıcısı, bölge çözümleyici, dosya depolama). EF Core'a veya herhangi bir altyapı teknolojisine bağımlı değildir.
- **Scootly.Infrastructure** — EF Core/PostgreSQL, outbox ve RabbitMQ, Redis, Identity/JWT, ödeme istemcisi, S3 uyumlu dosya depolama, sağlık kontrolleri.
- **Scootly.Api** — HTTP uçları, yetkilendirme politikaları, rate limiting, ProblemDetails hata yönetimi, SignalR, mesaj tüketicileri.
- **Scootly.Mvc** — web arayüzü (Razor, cookie tabanlı giriş); Api'ye ve ortak altyapıya bağlanır.
- **Scootly.Worker** — zamanlanmış işler ve saha operasyonu tüketicisi.
- **Scootly.PaymentSimulator / Scootly.DeviceSimulator** — dış ödeme sağlayıcısını ve cihaz ağ geçidini simüle eden yardımcı uygulamalar.

```
Sürüş bitir --> Ride.Complete (ücret) --> SaveChanges: veri + outbox (tek transaction)
   OutboxProcessor --(confirm)--> RabbitMQ scootly.events
      |- RideCompleted --> RideChargeConsumer --> PaymentSimulator (Idempotency-Key) --> Paid / ret
      |- VehicleStatusChanged --> her API instance --> SignalR (hizmet bölgesi grubu)
      '- VehicleBatteryLow --> Worker --> saha görevi
   PaymentSimulator --(imzalı webhook)--> /api/webhooks/payment-callback
   Worker: PendingPaymentRetryService --> reddedilen / kaybolan ödemeleri uzlaştırır
```

Kararların gerekçeleri `docs/adr/` klasöründedir. Bulut eşleştirmesi ve sistem tasarımı notları: `docs/architecture/system-design.md`.

---

## Teknoloji Yığını

| Katman | Teknoloji |
|---|---|
| Çalışma zamanı | .NET 10 |
| Veritabanı | PostgreSQL 16 |
| ORM | Entity Framework Core 10 (Npgsql) |
| Mesajlaşma | RabbitMQ 3.13 (RabbitMQ.Client 7) |
| Önbellek | Redis 7.4 (erişilemezse süreç içi yedek) |
| Kimlik | ASP.NET Core Identity + JWT Bearer |
| Dayanıklılık | Microsoft.Extensions.Http.Resilience (Polly) |
| Gerçek zamanlı | SignalR |
| Nesne depolama | SeaweedFS 4.48 (S3 API), istemci AWSSDK.S3 — isteğe bağlı (ADR 0043) |
| Ters vekil | Nginx 1.27 (TLS, mavi-yeşil upstream) |
| Gözlemlenebilirlik | Serilog + Seq 2025.1, OpenTelemetry Collector 0.116.1, Jaeger 1.65.0, Prometheus v3.1.0, Grafana 11.4.0 |
| Test | xUnit v3, Testcontainers (PostgreSQL, RabbitMQ), NetArchTest, Playwright (Chromium), k6 (yük testi) |
| API dokümantasyonu | Swagger / OpenAPI (sürüm başına doküman, JWT desteği) |
| Paket yönetimi | Central Package Management (`Directory.Packages.props`) |

---

## Proje Yapısı

```
Scootly/
|-- src/
|   |-- Scootly.Domain/            # İş kuralları, durum makineleri, tarife
|   |-- Scootly.Application/       # Use case'ler, soyutlamalar
|   |-- Scootly.Infrastructure/    # EF Core, outbox, RabbitMQ, Redis, Identity, depolama, migration'lar
|   |-- Scootly.Api/               # HTTP API, SignalR, tüketiciler (Dockerfile)
|   |-- Scootly.Mvc/               # Web arayüzü (Dockerfile)
|   |-- Scootly.Worker/            # Arka plan işleri (Dockerfile)
|   |-- Scootly.PaymentSimulator/  # Ödeme sağlayıcısı simülatörü (Dockerfile)
|   '-- Scootly.DeviceSimulator/   # Cihaz ağ geçidi simülatörü
|-- tests/
|   |-- Scootly.Testing/                 # Ortak test altyapısı (test sunucusu, test verisi)
|   |-- Scootly.Domain.UnitTests/
|   |-- Scootly.Application.UnitTests/
|   |-- Scootly.Infrastructure.Tests/    # Gerçek PostgreSQL + RabbitMQ ile altyapı testleri
|   |-- Scootly.Api.IntegrationTests/
|   |-- Scootly.Concurrency.Tests/       # Eşzamanlılık testleri + ölçümler/deneyler
|   |-- Scootly.Architecture.Tests/
|   |-- Scootly.E2E.Tests/               # Playwright ile tarayıcı testleri
|   '-- Scootly.LoadTests/               # Yük testleri
|-- deploy/
|   |-- docker-compose.yml         # Geliştirme yığını ve profiller
|   |-- docker-compose.prod.yml    # Üretim override'ı (Nginx, kapalı portlar, kaynak sınırları)
|   |-- docker-compose.green.yml   # Mavi-yeşil dağıtım için "green" kopyaları
|   |-- nginx/                     # Nginx yapılandırması ve upstream dosyaları
|   |-- .env.example               # Parola şablonu (.env git'e girmez)
|   '-- .env.prod.example          # Üretim şablonu
|-- .github/workflows/             # ci.yml (derleme, test, kapsam, paket taraması, imajlar), release.yml
`-- docs/
    |-- adr/                       # Mimari karar kayıtları
    |-- architecture/              # Sistem tasarımı, domain sözlüğü
    |-- backlog/                   # Teknik borç listesi, kullanıcı hikâyeleri
    |-- experiments/               # Ölçüm ve deney notları
    '-- runbook/                   # Dağıtım, arıza provaları, olay müdahalesi, postmortem
```

---

## Servisler, Portlar ve Profiller

Geliştirme Compose dosyası (`deploy/docker-compose.yml`) tüm profiller açıkken **15 servis** tanımlar (`docker compose --profile '*' config --services` ile sayıldı). Tüm portlar yalnızca `127.0.0.1`'e açılır.

| Profil | Servis | Port (127.0.0.1) | Not |
|---|---|---|---|
| (varsayılan) | postgres | 5432 | PostgreSQL 16 |
| (varsayılan) | redis | 6379 | Parolalı |
| (varsayılan) | rabbitmq | 5672, 15672 | AMQP ve yönetim arayüzü |
| `app` | migrator | — | EF Core migration'larını uygular, biter |
| `app` | payment-simulator | 5094 | Ödeme sağlayıcısı simülatörü |
| `app` | api | 5016 | Swagger: `/swagger` (Development) |
| `app` | mvc | 5096 | Web arayüzü |
| `app` | worker | — | Port yok; sağlık dosyası ile izlenir |
| `storage` | seaweedfs | 8333 | S3 API (isteğe bağlı) |
| `observability` | seq | 5341 | Log arayüzü |
| `observability` | jaeger | 16686 | İz arayüzü |
| `observability` | otel-collector | 4317, 4318 | OTLP |
| `observability` | prometheus | 9090 | Metrikler |
| `observability` | grafana | 3001 | Pano |
| `experiments` | redpanda | 9092 | Yalnızca Kafka deneyi için |

Sayım: varsayılan 3, `app` 5, `storage` 1, `observability` 5, `experiments` 1.

- **Üretim override'ı** (`docker-compose.prod.yml`) `nginx` ekler (16 servis) ve postgres, redis, rabbitmq, payment-simulator, api ve mvc portlarını kapatır. Tek giriş noktası `https://127.0.0.1:8443` (kendinden imzalı sertifika): `/` Mvc'ye, `/api/` Api'ye, `/hubs/` SignalR'a gider.
- **Mavi-yeşil** (`docker-compose.green.yml`), `green` profilinde `api-green` ve `mvc-green` kopyalarını tanımlar (dosyadan okundu; `GREEN_VERSION` tanımlı olmadan `config` çıktısı alınamaz). Prosedür: `docs/runbook/deployment.md` bölüm 8.
- Aynı sabit `container_name` değerleri yüzünden geliştirme ve üretim yığınları aynı anda çalışamaz.

`dotnet run` ile yerelde çalıştırıldığında portlar: Api `http://localhost:5016`, Mvc `http://localhost:5096`, PaymentSimulator `http://localhost:5094`.

---

## Kurulum

### Ön koşullar

- .NET 10 SDK
- Docker Desktop
- EF Core aracı: `dotnet tool install --global dotnet-ef`

### 1. Altyapıyı başlat

```
cd deploy
cp .env.example .env        # ardından .env içindeki parolaları güçlü rastgele değerlerle değiştirin
docker compose up -d        # PostgreSQL, Redis, RabbitMQ (yalnızca 127.0.0.1'e açılır)
cd ..
```

### 2. Sırları user-secrets'a ekle

Hiçbir sır kaynak kodda veya appsettings'te tutulmaz. Değerler `deploy/.env` ile tutarlı olmalıdır; JWT, cihaz ve webhook anahtarları en az 32 karakterdir.

```
# API
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=scootly;Username=postgres;Password=<POSTGRES_PASSWORD>" --project src/Scootly.Api
dotnet user-secrets set "Jwt:Key" "<en az 32 karakter>" --project src/Scootly.Api
dotnet user-secrets set "DeviceAuth:ClientSecret" "<en az 32 karakter>" --project src/Scootly.Api
dotnet user-secrets set "PaymentWebhook:Secret" "<en az 32 karakter>" --project src/Scootly.Api
dotnet user-secrets set "RabbitMq:Password" "<RABBITMQ_PASSWORD>" --project src/Scootly.Api
dotnet user-secrets set "Redis:ConnectionString" "localhost:6379,password=<REDIS_PASSWORD>" --project src/Scootly.Api
# İsteğe bağlı: ilk filo yöneticisi hesabı
dotnet user-secrets set "Bootstrap:FleetManagerEmail" "yonetici@ornek.com" --project src/Scootly.Api
dotnet user-secrets set "Bootstrap:FleetManagerPassword" "<güçlü parola>" --project src/Scootly.Api

# Worker
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "<API ile aynı>" --project src/Scootly.Worker
dotnet user-secrets set "RabbitMq:Password" "<RABBITMQ_PASSWORD>" --project src/Scootly.Worker
dotnet user-secrets set "Redis:ConnectionString" "localhost:6379,password=<REDIS_PASSWORD>" --project src/Scootly.Worker

# Simülatörler
dotnet user-secrets set "Webhook:Secret" "<PaymentWebhook:Secret ile aynı>" --project src/Scootly.PaymentSimulator
dotnet user-secrets set "ClientSecret" "<DeviceAuth:ClientSecret ile aynı>" --project src/Scootly.DeviceSimulator
```

Eksik veya kısa bir ayar olursa uygulama açılışta anlaşılır bir hata ile durur. Mvc'yi `dotnet run` ile yerelde çalıştırmak için gereken sırlar bu README'de belgelenmemiştir (bkz. [Teknik Borç](#teknik-borç)); tam yığın için aşağıdaki container yolu önerilir.

### 3. Veritabanı şemasını uygula

```
dotnet ef database update --project src/Scootly.Infrastructure --startup-project src/Scootly.Api
```

---

## Çalıştırma

Ayrı terminallerde:

```
dotnet run --project src/Scootly.PaymentSimulator   # http://localhost:5094
dotnet run --project src/Scootly.Api                # http://localhost:5016 (Swagger: /swagger)
dotnet run --project src/Scootly.Worker
dotnet run --project src/Scootly.DeviceSimulator    # API'de kayıtlı araçlar için telemetri gönderir
```

Sağlık kontrolleri: `GET /health/live` (süreç ayakta mı), `GET /health/ready` (PostgreSQL, Redis, RabbitMQ). Bağımlılık kesintisinde Docker healthcheck'i yalnızca `/health/live`'a baktığı için konteynerler `healthy` görünmeye devam edebilir; hazır olma durumunu `/health/ready` verir.

**Her şeyi container'da çalıştırmak** (migration ve Mvc dahil; `.env` içinde JWT/cihaz/webhook anahtarları da dolu olmalı):

```
cd deploy
docker compose --profile app up -d --build
```

Mvc `http://127.0.0.1:5096`, Api `http://127.0.0.1:5016` adresindedir.

Gözlemlenebilirlik yığınını eklemek için: `docker compose --profile observability up -d`. Container'daki Mvc'nin Seq/OpenTelemetry ayarlarının eksik olduğu 96. gün notunda kayıtlıdır; bu README yazılırken yeniden doğrulanmadı. Redpanda stream deneyi için: `docker compose --profile experiments up -d redpanda`.

---

## Saha Görevi Fotoğrafı (Nesne Depolama)

Saha operatörü bir görevi tamamlarken isteğe bağlı fotoğraf yükleyebilir. Özellik varsayılan olarak **kapalıdır** (`Storage:Enabled=false`); kapalıyken fotoğraf alanı görünmez ve uygulama eskisi gibi çalışır. Karar gerekçesi: `docs/adr/0043-nesne-depolama-seaweedfs.md` (MinIO topluluk sürümü arşivlendiği için SeaweedFS seçildi; istemci S3 API'si konuşur).

Etkinleştirmek için `deploy/.env` içinde:

```
STORAGE_ENABLED=true
STORAGE_ACCESS_KEY=<en az 16 karakter>
STORAGE_SECRET_KEY=<en az 16 karakter>
```

ve:

```
cd deploy
docker compose --profile storage up -d seaweedfs
docker compose --profile app up -d --build
```

Kurallar:

- En fazla 5 MB; yalnızca JPEG ve PNG. Tür, `Content-Type` veya dosya adına değil, içeriğin imzasına göre belirlenir.
- Nesne adı sunucuda üretilir (`field-tasks/{görev}/{guid}.jpg|png`); istemcinin gönderdiği ad kullanılmaz.
- Fotoğrafa erişim, yetkili kullanıcıya verilen 60 saniyelik ön-imzalı URL ile olur (`Cache-Control: no-store`).
- Görüntü çözümleme, kötü amaçlı yazılım taraması ve EXIF (konum) temizleme **yoktur**.
- Depo üretim yığınına bağlanmamıştır (Nginx'te `client_max_body_size` tanımlı değildir, ön-imzalı URL'nin ana makine adı dışarıdan erişilemez). Ayrıntı: `docs/backlog/technical-debt.md`.

---

## Test ve Kapsam

```
dotnet test                                                        # tümü
dotnet test --filter "Category=E2E"                                       # yalnızca tarayıcı testleri (Docker + Chromium gerekir)
dotnet test --filter "Category=Resilience"                                # RabbitMQ kesinti/kurtarma testi (Docker, yaklaşık 1 dk)
dotnet test --filter "Category!=Measurement&Category!=Experiment&Category!=E2E"  # CI'daki hızlı set
```

- **Domain.UnitTests** — iş kuralları ve durum makineleri, dış bağımlılık yok.
- **Application.UnitTests** — handler'lar, EF'nin yeniden yükleme/çakışma davranışını taklit eden sahte repository'lerle.
- **Infrastructure.Tests** — gerçek PostgreSQL ve RabbitMQ container'larıyla: outbox yayını, retry → DLQ akışı, kısıt ve eşzamanlılık istisnaları, webhook imzası, ödeme istemcisi, depolama kuralları. `Category=Resilience` testi kendi RabbitMQ container'ını durdurup başlatır ve kesinti sırasında bağlantı sağlayıcıya dokunulsa bile tüketicinin toparlandığını doğrular (106. gün bulgusunun regresyon testi).
- **Api.IntegrationTests** — gerçek PostgreSQL üzerinde uçtan uca HTTP akışları ve güvenlik regresyonları (IDOR, token ayrımı, kilitleme, rate limit, webhook sahteciliği).
- **Concurrency.Tests** — eşzamanlı rezervasyon, izolasyon seviyesi, deadlock, N+1; `Category=Measurement` testleri yalnızca süre raporlar.
- **E2E.Tests** — Playwright (Chromium) ile tarayıcıdan giriş, araç düzenleme, saha görevi üstlenme ve fotoğraflı tamamlama akışları. Geçici bir PostgreSQL ile gerçek bir `Scootly.Mvc` süreci başlatır (Development ortamı, http). İlk kullanımdan önce derleyip bir kez tarayıcı kurmak gerekir: `dotnet build tests/Scootly.E2E.Tests` ardından `powershell -File tests/Scootly.E2E.Tests/bin/Debug/net10.0/playwright.ps1 install chromium` (CI'da `pwsh ... install --with-deps chromium`). `dotnet test` filtresiz çalıştırıldığında bu testleri de çalıştırır; CI'da ayrı bir `e2e` işidir.
- **Architecture.Tests** — katman bağımlılık kuralları.
- **LoadTests** — yük testleri; düzen ve ilk sonuçlar: `docs/adr/0039-k6-yuk-testi-duzeni-ve-ilk-sonuclar.md`, `docs/experiments/load-test-2026-10-06.md`.

Integration, Infrastructure ve Concurrency testleri Docker gerektirir; geliştiricinin user-secrets'ına veya çalışan Redis/RabbitMQ'suna ihtiyaç duymaz (sırlar test başına üretilir).

**Gerçek depoya karşı testler.** Fotoğraf yükleme için canlı depo testi (`StorageLiveTests`) ve E2E fotoğraf senaryoları, aşağıdaki üç ortam değişkeni tanımlıysa çalışır; tanımlı değilse atlanır (CI bu yüzden etkilenmez):

```
SCOOTLY_LIVE_STORAGE_ENDPOINT, SCOOTLY_LIVE_STORAGE_ACCESS_KEY, SCOOTLY_LIVE_STORAGE_SECRET_KEY
```

**Kapsam.** `dotnet test --collect:"XPlat Code Coverage"` ile alınır; CI aynısını yapıp ReportGenerator ile özet üretir (EF migration'ları ve test assembly'leri hariç).

- Ölçüm (6 Ekim 2026, yalnızca `tests/Scootly.Domain.UnitTests`, yalnızca `Scootly.Domain` paketi): satır kapsamı **%90,9** (527/580), dal kapsamı **%91,7**.
- CI'daki birleşik rapor (tüm projeler) 105. günde satır %73,7, dal %60,6 idi; güncel birleşik ölçüm bu README'ye işlenmemiştir. Kapsamı %0 olan sınıflar için `docs/backlog/technical-debt.md` (101. ve 105. gün notları).
- E2E testleri, Mvc ayrı süreçte çalıştığı için kapsam raporuna girmez.

---

## API Uçları ve Web Arayüzü

| Uç | Açıklama | Yetki |
|---|---|---|
| POST /api/auth/register | Kullanıcı kaydı (Driver rolü) | Herkese açık, IP başına sınırlı |
| POST /api/auth/login | Giriş, JWT döner (5 hatalı denemede kilit) | Herkese açık, IP başına sınırlı |
| POST /api/device-auth/token | Cihaz token'ı | Herkese açık, IP başına sınırlı |
| GET /api/v1/vehicles | Araç listesi (filtreli, sayfalı, önbellekli) | Herkese açık |
| GET /api/v2/vehicles | Araç listesi (model bilgisiyle) | Herkese açık |
| GET /api/v1/vehicles/{id} | Araç ayrıntısı | Herkese açık |
| POST /api/v1/vehicles | Araç kaydı | FleetManager |
| POST /api/v1/vehicles/{id}/reserve | Rezervasyon | Driver |
| DELETE /api/v1/vehicles/{id}/reservation | Kendi rezervasyonunu iptal | Driver |
| POST /api/v1/vehicles/{id}/maintenance | Bakıma al | FleetManager / FieldOperator |
| POST /api/v1/vehicles/{id}/return-to-service | Hizmete döndür | FleetManager / FieldOperator |
| POST /api/rides/start | Sürüş başlat (201 + rideId) | Driver (rezervasyon sahibi) |
| POST /api/rides/{id}/complete | Sürüş bitir (202, ödeme asenkron) | Sürüş sahibi |
| GET /api/rides/active | Aktif sürüşüm | Driver |
| GET /api/rides/{id} | Sürüş ayrıntısı | Sürüş sahibi |
| GET /api/rides/{id}/payment-status | Ödeme durumu | Sürüş sahibi |
| POST /api/telemetry/batch | Toplu telemetri (en fazla 500 okuma) | Cihaz |
| POST /api/webhooks/payment-callback | Ödeme sağlayıcısı bildirimi | HMAC imzası |
| GET/POST /api/v1/service-areas | Hizmet bölgeleri | Okuma herkese açık / oluşturma FleetManager |
| POST/DELETE /api/v1/admin/users/{id}/roles/{rol} | Rol yönetimi | FleetManager |
| /hubs/fleet | SignalR canlı araç durumu | Oturum açmış kullanıcı |

Hatalar RFC 7807 ProblemDetails biçiminde döner. Tüm şemalar için Swagger arayüzüne bakınız.

**Web arayüzü (Mvc)** denetleyicileri: Account (giriş/çıkış), Dashboard, Home, Map (canlı harita), Vehicles (liste ve düzenleme), Rides, FieldTasks (görev üstlenme, tamamlama, fotoğraf). Driver rolü saha görevleri sayfasına giremez (403/AccessDenied).

---

## Güvenlik Notları

- Git geçmişindeki eski anahtar ve parolalar (28. gün öncesi) 27.09.2026'da yenilenmiştir ve geçersizdir. Yeni bir ortam kurarken asla bu değerleri veya `.env.example`'daki yer tutucuları kullanmayın. Ayrıntılar: ADR 0021 (güvenlik sertleştirmesi) ve `docs/runbook/postmortems/2026-09-27-sizmis-sirlar.md`.
- GitHub "Secret Protection" ve "Push protection" ayarları depo ayar ekranında etkin görünmektedir (6 Ekim 2026). Gerçek bir sızıntı denemesiyle doğrulanmamıştır; CI'da ayrıca bir secret taraması yoktur.
- Fotoğraf yükleme: boyut ve tür doğrulaması sunucuda yapılır, nesne adı sunucuda üretilir, erişim kısa ömürlü ön-imzalı URL iledir. İçerik taraması ve EXIF temizleme yoktur (bkz. [Saha Görevi Fotoğrafı](#saha-görevi-fotoğrafı-nesne-depolama)).
- Olay müdahalesi ve sır rotasyonu için: `docs/runbook/incident-response.md`.

---

## Dağıtım ve İşletme

| Belge | Konu |
|---|---|
| `docs/runbook/deployment.md` | Sürümler, yedek ve geri yükleme provası, geri alma, mavi-yeşil dağıtım, ortam yönetimi |
| `docs/runbook/failure-drills.md` | Kasıtlı arıza provaları (ADR 0042) |
| `docs/runbook/incident-response.md` | Olay müdahalesi önerisi (hiç tatbikat yapılmadı) |
| `docs/runbook/postmortem-template.md` | Postmortem şablonu |
| `docs/architecture/system-design.md` | Bulut eşleştirme tablosu ve sistem tasarımı |
| `CHANGELOG.md` | Sürüm notları; `v*` etiketi `release.yml` hattını tetikler |

---

## Mimari Kararlar

`docs/adr/` klasöründe, gerekçeleri ve güncellemeleriyle birlikte (başlıklar dosya adlarından yazılmıştır):

| No | Konu |
|---|---|
| 0001 | Katmanlı mimari (Clean Architecture) seçimi |
| 0002 | Repository kullanım sınırları |
| 0003 | Test stratejisi |
| 0004 | Konum verisi saklama süresi (uygulandı) |
| 0005 | Eşzamanlılık stratejisi (xmin) |
| 0006 | İndeksleme gözlemleri |
| 0007 | Transaction sınırı disiplini |
| 0008 | N+1 sorgu önleme |
| 0009 | Toplu yazma stratejisi |
| 0010 | Idempotency yaklaşımı |
| 0011 | Worker hata toleransı |
| 0012 | Telemetri kuyruk mimarisi |
| 0013 | Mesajlaşma dayanıklılığı — retry ve DLQ (düzeltildi) |
| 0014 | Paralel işleme deneyi |
| 0015 | Ödeme saga'sı — koreografi kararları |
| 0016 | RabbitMQ vs Redpanda deneyimi |
| 0017 | Modüler monolit durum değerlendirmesi |
| 0018 | Dikey dilim deneyimi |
| 0019 | Ride aggregate sınır incelemesi |
| 0020 | Faz 4 desen envanteri |
| 0021 | Güvenlik sertleştirmesi — sırlar, kimlikler, erişim sınırları |
| 0022 | Mesajlaşma güvenilirliği — domain olayları, outbox, tüketiciler |
| 0023 | Ödeme durumu, tarife ve çift tahsilat önleme |
| 0024 | Telemetrinin araç durumunu güncellemesi |
| 0025 | FieldOps bağlamı |
| 0026 | Arayüz yetkilendirmesi |
| 0027 | Mvc–Api arayüz kimliği |
| 0028 | Mvc güvenlik kararları |
| 0029 | Faz 5 ilk yarı retro |
| 0030 | Metrik hattı |
| 0031 | Sağlık kontrolleri ve panolar |
| 0032 | Çok aşamalı imaj derlemesi |
| 0033 | Üretime hazır imaj ve kapanış davranışı |
| 0034 | Üretim Compose yapılandırması |
| 0035 | Nginx ters vekil ve TLS |
| 0036 | Linux ve 500 araç yükü |
| 0037 | CI kapsam, önbellek ve kararsız test |
| 0038 | Sürüm hattı ve GHCR yayını |
| 0039 | k6 yük testi düzeni ve ilk sonuçlar |
| 0040 | Playwright E2E testleri |
| 0041 | Replika ve mavi-yeşil dağıtım |
| 0042 | Kasıtlı arıza provası |
| 0043 | Nesne depolama için SeaweedFS (S3 API), MinIO değil |

---

## Teknik Borç

Bilinçli olarak ertelenmiş işler, açık kalanlar ve kapatılanların geçmişi `docs/backlog/technical-debt.md` dosyasındadır. Dosyanın başındaki "Gün 109: son durum" bölümü her açık maddeyi "bilinçli bırakıldı" (gerekçesiyle) veya "açık" olarak ayırır.

---

## Lisans

Bu proje bir öğrenme/portföy çalışmasıdır.
