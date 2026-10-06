# Scootly

**Şehir içi elektrikli scooter paylaşım platformunun backend'i.** Clean Architecture prensipleriyle, .NET 10 ile geliştirilmiştir.

---

## İçindekiler

- [Genel Bakış](#genel-bakış)
- [Mimari](#mimari)
- [Teknoloji Yığını](#teknoloji-yığını)
- [Proje Yapısı](#proje-yapısı)
- [Kurulum](#kurulum)
- [Çalıştırma](#çalıştırma)
- [Test](#test)
- [API Uçları](#api-uçları)
- [Güvenlik Notları](#güvenlik-notları)
- [Mimari Kararlar](#mimari-kararlar)
- [Teknik Borç](#teknik-borç)

---

## Genel Bakış

Scootly, kullanıcıların yakınlarındaki elektrikli scooter'ları görüp kiralayabildiği, sürüş başlatıp bitirebildiği bir micro-mobility platformudur:

- **Kimlik ve yetki** — JWT ile giriş, rol tabanlı (Driver, FleetManager, FieldOperator) ve kaynak sahipliği bazlı yetkilendirme; cihazlar için ayrı istemci kimlik bilgisi akışı; hesap kilitleme ve istemci başına rate limiting.
- **Araç ve sürüş yönetimi** — durum makinesiyle korunan araç yaşam döngüsü; rezervasyon sahipliği; sürücü başına tek aktif rezervasyon ve tek aktif sürüş (uygulama + veritabanı kısıtları).
- **Eşzamanlılık güvenliği** — PostgreSQL `xmin` ile iyimser kilitleme; çakışmalarda taze veriyle otomatik yeniden deneme.
- **Ödeme saga'sı** — ücret domain'de tarifeyle hesaplanır; tahsilat outbox → RabbitMQ → ödeme sağlayıcısı akışıyla asenkron yapılır; idempotency anahtarı, imzalı webhook ve uzlaştırma işiyle çift tahsilat ve kayıp önlenir.
- **Güvenilir mesajlaşma** — domain olayları aynı transaction'da outbox'a yazılır; publisher confirm, gecikmeli retry kuyruğu ve ölü mektup kuyruğu (DLQ).
- **Telemetri** — cihazlardan toplu konum/batarya verisi; aracın son bilinen durumu güncellenir, batarya eşiği aşılınca saha görevi üretilir.
- **Canlı bildirimler** — SignalR ile, hizmet bölgesine göre gruplanmış araç durumu değişiklikleri.
- **Arka plan işleri** — süresi dolan rezervasyonlar, terk edilmiş sürüşler, bekleyen ödemelerin yeniden denenmesi, veri saklama politikası (KVKK).

---

## Mimari

Proje **Clean Architecture** (katmanlı mimari) prensiplerine göre kurulmuştur. Bağımlılık oku her zaman içe, Domain'e doğrudur ve mimari testlerle denetlenir:

```
Scootly.Api / Scootly.Worker
        |
Scootly.Infrastructure
        |
Scootly.Application
        |
Scootly.Domain   (hiçbir dış pakete bağımlı değildir)
```

- **Scootly.Domain** — iş kuralları, durum makineleri, değer nesneleri, tarife, domain olayları.
- **Scootly.Application** — use case'ler (komut/handler), soyutlamalar (repository, saat, ödeme sağlayıcısı, bölge çözümleyici). EF Core'a veya herhangi bir altyapı teknolojisine bağımlı değildir.
- **Scootly.Infrastructure** — EF Core/PostgreSQL, outbox ve RabbitMQ, Redis, Identity/JWT, ödeme istemcisi, sağlık kontrolleri.
- **Scootly.Api** — HTTP uçları, yetkilendirme politikaları, rate limiting, ProblemDetails hata yönetimi, SignalR, mesaj tüketicileri.
- **Scootly.Worker** — zamanlanmış işler ve saha operasyonu tüketicisi.
- **Scootly.PaymentSimulator / Scootly.DeviceSimulator** — dış ödeme sağlayıcısını ve cihaz ağ geçidini simüle eden yardımcı uygulamalar.

```
Sürüş bitir ──▶ Ride.Complete (ücret) ──▶ SaveChanges: veri + outbox (tek transaction)
   OutboxProcessor ──(confirm)──▶ RabbitMQ scootly.events
      ├─ RideCompleted ──▶ RideChargeConsumer ──▶ PaymentSimulator (Idempotency-Key) ──▶ Paid / ret
      ├─ VehicleStatusChanged ──▶ her API instance ──▶ SignalR (hizmet bölgesi grubu)
      └─ VehicleBatteryLow ──▶ Worker ──▶ saha görevi
   PaymentSimulator ──(imzalı webhook)──▶ /api/webhooks/payment-callback
   Worker: PendingPaymentRetryService ──▶ reddedilen / kaybolan ödemeleri uzlaştırır
```

Kararların gerekçeleri `docs/adr/` klasöründedir.

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
| Test | xUnit v3, Testcontainers (PostgreSQL, RabbitMQ), NetArchTest |
| Loglama | Serilog (hassas alan maskeleme) |
| API dokümantasyonu | Swagger / OpenAPI (sürüm başına doküman, JWT desteği) |
| Paket yönetimi | Central Package Management (`Directory.Packages.props`) |

---

## Proje Yapısı

```
Scootly/
├── src/
│   ├── Scootly.Domain/            # İş kuralları, durum makineleri, tarife
│   ├── Scootly.Application/       # Use case'ler, soyutlamalar
│   ├── Scootly.Infrastructure/    # EF Core, outbox, RabbitMQ, Redis, Identity, migration'lar
│   ├── Scootly.Api/               # HTTP API, SignalR, tüketiciler (Dockerfile)
│   ├── Scootly.Worker/            # Arka plan işleri (Dockerfile)
│   ├── Scootly.PaymentSimulator/  # Ödeme sağlayıcısı simülatörü (Dockerfile)
│   └── Scootly.DeviceSimulator/   # Cihaz ağ geçidi simülatörü
├── tests/
│   ├── Scootly.Testing/                 # Ortak test altyapısı (test sunucusu, test verisi)
│   ├── Scootly.Domain.UnitTests/
│   ├── Scootly.Application.UnitTests/
│   ├── Scootly.Infrastructure.Tests/    # Gerçek PostgreSQL + RabbitMQ ile altyapı testleri
│   ├── Scootly.Api.IntegrationTests/
│   ├── Scootly.Concurrency.Tests/       # Eşzamanlılık testleri + ölçümler/deneyler
│   ├── Scootly.Architecture.Tests/
│   └── Scootly.E2E.Tests/               # Playwright ile tarayici testleri (Gun 104)
├── deploy/
│   ├── docker-compose.yml         # PostgreSQL, Redis, RabbitMQ (+ app ve experiments profilleri)
│   └── .env.example               # Parola şablonu (.env git'e girmez)
├── .github/workflows/ci.yml       # Derleme, test, paket güvenlik taraması, Docker imajları
└── docs/
    ├── adr/                       # Mimari karar kayıtları
    ├── architecture/              # Domain sözlüğü
    └── backlog/                   # Teknik borç listesi
```

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

Eksik veya kısa bir ayar olursa uygulama açılışta anlaşılır bir hata ile durur.

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

Sağlık kontrolleri: `GET /health/live` (süreç ayakta mı), `GET /health/ready` (PostgreSQL, Redis, RabbitMQ).

**Her şeyi container'da çalıştırmak** (migration dahil; `.env` içinde JWT/cihaz/webhook anahtarları da dolu olmalı):

```
cd deploy
docker compose --profile app up -d --build
```

Redpanda stream deneyi için: `docker compose --profile experiments up -d redpanda`.

---

## Test

```
dotnet test                                                        # tümü
dotnet test --filter "Category=E2E"                                       # yalnizca tarayici testleri (Docker + Chromium gerekir)
dotnet test --filter "Category!=Measurement&Category!=Experiment&Category!=E2E"  # CI'daki hızlı set
```

- **Domain.UnitTests** — iş kuralları ve durum makineleri, dış bağımlılık yok.
- **Application.UnitTests** — handler'lar, EF'nin yeniden yükleme/çakışma davranışını taklit eden sahte repository'lerle.
- **Infrastructure.Tests** — gerçek PostgreSQL ve RabbitMQ container'larıyla: outbox yayını, retry → DLQ akışı, kısıt ve eşzamanlılık istisnaları, webhook imzası, ödeme istemcisi.
- **Api.IntegrationTests** — gerçek PostgreSQL üzerinde uçtan uca HTTP akışları ve güvenlik regresyonları (IDOR, token ayrımı, kilitleme, rate limit, webhook sahteciliği).
- **Concurrency.Tests** — eşzamanlı rezervasyon, izolasyon seviyesi, deadlock, N+1; `Category=Measurement` testleri yalnızca süre raporlar.
- **E2E.Tests** - Playwright (Chromium) ile tarayicidan giris, arac duzenleme ve saha gorevi ustlenme akislari. Gecici bir PostgreSQL ile gercek bir Scootly.Mvc sureci baslatir (Development ortami, http). Ilk kullanimdan once derleyip bir kez tarayici kurmak gerekir: `dotnet build tests/Scootly.E2E.Tests` ardindan `powershell -File tests/Scootly.E2E.Tests/bin/Debug/net10.0/playwright.ps1 install chromium` (CI'da `pwsh ... install --with-deps chromium`). `dotnet test` filtresiz calistirildiginda bu testleri de calistirir; CI'da ayri bir `e2e` isidir.
- **Architecture.Tests** — katman bağımlılık kuralları.

Integration, Infrastructure ve Concurrency testleri Docker gerektirir; geliştiricinin user-secrets'ına veya çalışan Redis/RabbitMQ'suna ihtiyaç duymaz (sırlar test başına üretilir).

---

## API Uçları

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

---

## Güvenlik Notları

- Git geçmişindeki eski anahtar ve parolalar (28. gün öncesi) 27.09.2026'da yenilenmiştir ve geçersizdir. Yeni bir ortam kurarken asla bu değerleri veya `.env.example`'daki yer tutucuları kullanmayın.
- Ayrıntılar: ADR 0021 (güvenlik sertleştirmesi).

---

## Mimari Kararlar

`docs/adr/` klasöründe, gerekçeleri ve güncellemeleriyle birlikte:

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

---

## Teknik Borç

Bilinçli olarak ertelenmiş işler ve kapatılanların geçmişi `docs/backlog/technical-debt.md` dosyasındadır.

---

## Lisans

Bu proje bir öğrenme/portföy çalışmasıdır.
