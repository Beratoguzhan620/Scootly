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
- [Mimari Kararlar](#mimari-kararlar)
- [Teknik Borç](#teknik-borç)

---

## Genel Bakış

Scootly, kullanıcıların yakınlarındaki elektrikli scooter'ları görüp kiralayabildiği, sürüş başlatıp bitirebildiği bir micro-mobility platformudur. Proje şu an aktif geliştirme aşamasındadır ve aşağıdaki temel yetenekleri içerir:

- **Kimlik doğrulama ve yetkilendirme** — JWT tabanlı kullanıcı girişi, rol ve kaynak sahipliği bazlı yetki kontrolü, ayrıca cihazlar (scooter'lar) için ayrı bir client-credentials akışı.
- **Araç ve sürüş yönetimi** — durum makinesi ile korunan araç yaşam döngüsü (Available → Reserved → InRide → Available), eşzamanlılık korumalı rezervasyon sistemi.
- **Eşzamanlılık güvenliği** — PostgreSQL'in xmin sistem sütunu üzerinden iyimser eşzamanlılık kontrolü; 50-100 eşzamanlı istek altında test edilip doğrulanmıştır.
- **Performans optimizasyonları** — indeksleme, N+1 sorgu önleme, AsNoTracking, toplu yazma (PostgreSQL COPY BINARY, ~90x hızlanma), Redis tabanlı cache-aside deseni.
- **Arka plan servisleri** — süresi dolan rezervasyonları iptal etme, düşük bataryalı araçları tarama, terk edilmiş sürüşleri tespit etme.
- **Telemetri altyapısı** — araçlardan toplu konum/batarya verisi kabul eden bir alım hattı.

---

## Mimari

Proje, **Clean Architecture** (katmanlı mimari) prensiplerine göre kurulmuştur. Bağımlılık oku her zaman içe, Domain'e doğrudur:

```
Scootly.Api / Scootly.Worker
        |
Scootly.Infrastructure
        |
Scootly.Application
        |
Scootly.Domain   (hicbir dis pakete bagimli degildir)
```

- **Scootly.Domain** — iş kuralları, durum makineleri, değer nesneleri (value object'ler). Hiçbir framework veya veritabanı bilgisine sahip değildir.
- **Scootly.Application** — use case'ler (komut/handler çiftleri), arayüzler (repository, cache, saat, kullanıcı bağlamı soyutlamaları).
- **Scootly.Infrastructure** — EF Core, PostgreSQL, Redis, Identity, JWT üretimi gibi somut teknik uygulamalar.
- **Scootly.Api** — HTTP uçları, kimlik doğrulama/yetkilendirme pipeline'ı, sürümleme, rate limiting.
- **Scootly.Worker** — arka planda sürekli çalışan, zamanlanmış görevler (BackgroundService tabanlı).

Bu ayrımın gerekçeleri docs/adr/ klasöründeki karar kayıtlarında (ADR) belgelenmiştir.

---

## Teknoloji Yığını

| Katman | Teknoloji |
|---|---|
| Çalışma zamanı | .NET 10 |
| Veritabanı | PostgreSQL 16 (Docker) |
| Önbellek | Redis 7 (Docker) |
| ORM | Entity Framework Core |
| Kimlik doğrulama | ASP.NET Core Identity + JWT Bearer |
| Test | xUnit, Testcontainers |
| Loglama | Serilog |
| API dokümantasyonu | Swagger / OpenAPI |
| Sürümleme | Asp.Versioning |

---

## Proje Yapısı

```
scootly/
├── src/
│   ├── Scootly.Domain/          # İş kuralları, durum makineleri
│   ├── Scootly.Application/     # Use case'ler, arayüzler
│   ├── Scootly.Infrastructure/  # EF Core, Redis, Identity, JWT
│   ├── Scootly.Api/             # HTTP API
│   └── Scootly.Worker/          # Arka plan servisleri
├── tests/
│   ├── Scootly.Domain.UnitTests/
│   ├── Scootly.Application.UnitTests/
│   ├── Scootly.Api.IntegrationTests/
│   └── Scootly.Concurrency.Tests/
├── deploy/
│   └── docker-compose.yml       # PostgreSQL + Redis
└── docs/
    ├── adr/                     # Mimari karar kayıtları
    └── backlog/                 # Teknik borç listesi
```

---

## Kurulum

### Ön koşullar

- .NET 10 SDK (https://dotnet.microsoft.com/download)
- Docker Desktop (https://www.docker.com/products/docker-desktop/)

### Adımlar

1. Depoyu klonla:
```
git clone https://github.com/Beratoguzhan620/Scootly.git
cd Scootly
```

2. PostgreSQL ve Redis'i ayağa kaldır:
```
cd deploy
docker compose up -d
cd ..
```

3. Bağlantı bilgilerini yerel sır deposuna ekle (appsettings.json'da bulunmaz, bkz. Mimari Kararlar):
```
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=scootly;Username=postgres;Password=sifre123" --project src/Scootly.Api
dotnet user-secrets set "Jwt:Key" "<en az 32 karakterlik gizli anahtar>" --project src/Scootly.Api
dotnet user-secrets set "DeviceAuth:ClientSecret" "<cihaz gizli anahtarı>" --project src/Scootly.Api
```

4. Veritabanı şemasını uygula:
```
dotnet ef database update --project src/Scootly.Infrastructure --startup-project src/Scootly.Api
```

5. Bağımlılıkları geri yükle ve derle:
```
dotnet build
```

---

## Çalıştırma

**API'yi başlat:**
```
dotnet run --project src/Scootly.Api
```
Swagger arayüzü: http://localhost:5016/swagger

**Arka plan servislerini başlat:**
```
dotnet run --project src/Scootly.Worker
```

---

## Test

Proje dört ayrı test katmanı içerir:

```
dotnet test
```

- **Scootly.Domain.UnitTests** — iş kurallarını, hiçbir dış bağımlılık olmadan test eder.
- **Scootly.Application.UnitTests** — handler'ları sahte (fake) repository'lerle izole test eder.
- **Scootly.Api.IntegrationTests** — Testcontainers ile geçici bir PostgreSQL container'ına karşı, gerçek HTTP akışlarını uçtan uca test eder.
- **Scootly.Concurrency.Tests** — eşzamanlılık, deadlock, indeksleme ve performans senaryolarını doğrular.

> Not: Integration ve Concurrency testleri Docker'ın çalışıyor olmasını gerektirir.

---

## API Uçları

| Uç | Açıklama | Yetki |
|---|---|---|
| POST /api/auth/register | Kullanıcı kaydı | Herkese açık |
| POST /api/auth/login | Giriş, JWT token döner | Herkese açık |
| POST /api/device-auth/token | Cihaz kimlik doğrulama | Herkese açık |
| GET /api/v1/vehicles | Araçları listele (filtreli, sayfalı, cache'li) | Herkese açık |
| POST /api/v1/vehicles | Yeni araç kaydet | Filo Yöneticisi |
| POST /api/v1/vehicles/{id}/reserve | Araç rezerve et | Giriş yapmış kullanıcı |
| POST /api/rides/start | Sürüş başlat | Giriş yapmış kullanıcı |
| POST /api/rides/{id}/complete | Sürüş bitir | Giriş yapmış kullanıcı |
| GET /api/rides/{id} | Sürüş detayını görüntüle | Yalnızca sürüş sahibi |
| POST /api/telemetry/batch | Toplu telemetri verisi gönder | Giriş yapmış kullanıcı/cihaz |

Tüm uçların tam listesi ve şemaları için Swagger arayüzüne bakınız.

---

## Mimari Kararlar

Önemli mimari kararlar, gerekçeleriyle birlikte docs/adr/ klasöründe kayıtlıdır:

- **0001** — Katmanlı mimari (Clean Architecture) seçimi
- **0002** — Repository kullanım sınırları
- **0003** — Test stratejisi (unit/integration ayrımı)
- **0004** — Konum verisi saklama süresi
- **0005** — Eşzamanlılık stratejisi (iyimser kilitleme, xmin)
- **0006** — İndeksleme gözlemleri
- **0007** — Transaction sınırı disiplini
- **0008** — N+1 sorgu önleme
- **0009** — Toplu yazma stratejisi
- **0010** — Idempotency yaklaşımı (ön karar)

---

## Teknik Borç

Bilinçli olarak ertelenmiş veya kısmen tamamlanmış işler docs/backlog/technical-debt.md dosyasında şeffaf şekilde kayıt altındadır.

---

## Lisans

Bu proje bir öğrenme/portföy çalışmasıdır.