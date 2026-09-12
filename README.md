# Scootly

Şehir içi elektrikli scooter paylaşım sisteminin backend'i. Clean Architecture
prensipleriyle, .NET ile geliştirilmektedir.

## Gereksinimler

- .NET 10 SDK
- PostgreSQL 16+ (yerel kurulum veya `deploy/docker-compose.yml`)

## Geliştirme ortamı kurulumu

Bu depoda **hiçbir parola, bağlantı dizesi veya imza anahtarı commit edilmez.**
Hassas değerler .NET'in user-secrets mekanizmasında tutulur; bu değerler proje
klasöründe değil, kullanıcı profilinde saklanır ve Git'e hiç uğramaz.

İlk kurulumda bir kez:

```bash
dotnet user-secrets init --project src/Scootly.Api
```

### 1. Veritabanı bağlantısı

```bash
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Host=localhost;Port=5432;Database=scootly;Username=scootly_app;Password=KENDI-PAROLAN" \
  --project src/Scootly.Api
```

### 2. JWT imza anahtarı (zorunlu)

Anahtar yoksa veya 32 bayttan kısaysa **uygulama hiç başlamaz**. Bu bilinçli:
zayıf anahtarla çalışan bir API, herkesin kendine yönetici token'ı üretebilmesi
demektir.

Anahtarı uydurma, üret:

```bash
dotnet user-secrets set "Jwt:SigningKey" \
  "$(LC_ALL=C tr -dc 'A-Za-z0-9' </dev/urandom | head -c 64)" \
  --project src/Scootly.Api
```

`Jwt:Issuer`, `Jwt:Audience` ve `Jwt:AccessTokenLifetimeMinutes` gizli değil;
`appsettings.json` içinde duruyor.

### 3. Test kullanıcısı (isteğe bağlı)

Bu ikisi girilmezse test kullanıcısı **hiç oluşturulmaz** — yalnızca roller oluşur:

```bash
dotnet user-secrets set "Seed:TestUser:Email" "surucu@scootly.local" --project src/Scootly.Api
dotnet user-secrets set "Seed:TestUser:Password" "KENDI-TEST-PAROLAN" --project src/Scootly.Api
```

Parola politikası: en az 12 karakter, büyük harf, küçük harf, rakam ve
alfanümerik olmayan en az birer karakter. Beş hatalı denemeden sonra hesap
15 dakika kilitlenir.

## Çalıştırma

```bash
dotnet build
dotnet ef database update --project src/Scootly.Infrastructure --startup-project src/Scootly.Api
dotnet run --project src/Scootly.Api
```

Kimlik uçlarını denemek için `src/Scootly.Api/Auth.http` dosyasını Visual Studio'da
açıp istekleri tek tek çalıştırabilirsin.

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
