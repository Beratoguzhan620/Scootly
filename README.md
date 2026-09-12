# Scootly

Şehir içi elektrikli scooter paylaşım sisteminin backend'i. Clean Architecture
prensipleriyle, .NET ile geliştirilmektedir.

## Gereksinimler

- .NET 10 SDK
- PostgreSQL 16+ (yerel kurulum veya `deploy/docker-compose.yml`)

## Geliştirme ortamı kurulumu

Bu depoda **hiçbir parola veya bağlantı dizesi commit edilmez.** Hassas değerler
.NET'in user-secrets mekanizmasında tutulur; bu değerler proje klasöründe değil,
kullanıcı profilinde saklanır ve Git'e hiç uğramaz.

İlk kurulumda bir kez:

```bash
dotnet user-secrets init --project src/Scootly.Api
```

Sonra kendi yerel değerlerini gir:

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Host=localhost;Port=5432;Database=scootly;Username=scootly_app;Password=KENDI-PAROLAN" \
  --project src/Scootly.Api
```

İsteğe bağlı — geliştirme ortamında otomatik oluşturulacak test kullanıcısı.
**Bu ikisi girilmezse test kullanıcısı hiç oluşturulmaz** (yalnızca roller oluşur):

```bash
dotnet user-secrets set "Seed:TestUser:Email" "surucu@scootly.local" --project src/Scootly.Api
dotnet user-secrets set "Seed:TestUser:Password" "KENDI-TEST-PAROLAN" --project src/Scootly.Api
```

Parola politikası (21. gün): en az 12 karakter, büyük harf, küçük harf, rakam ve
alfanümerik olmayan en az birer karakter.

## Çalıştırma

```bash
dotnet build
dotnet ef database update --project src/Scootly.Infrastructure --startup-project src/Scootly.Api
dotnet run --project src/Scootly.Api
```

## Testler

```bash
dotnet test
```

`Scootly.Api.IntegrationTests` gerçek bir PostgreSQL konteyneri başlatır
(Testcontainers); çalışması için Docker gerekir. Birim testleri Docker gerektirmez:

```bash
dotnet test tests/Scootly.Domain.UnitTests
dotnet test tests/Scootly.Application.UnitTests
dotnet test tests/Scootly.Infrastructure.UnitTests
```

## Mimari kararlar

Verilmiş kararlar ve gerekçeleri `docs/adr/` altında. Bilinçli bırakılan eksikler
`docs/backlog/technical-debt.md` dosyasında.
