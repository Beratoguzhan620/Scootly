using System.Net.Mail;
using Scootly.Api.Contracts.Requests;
using Scootly.Application.Abstractions;
using Scootly.Domain.Fleet;
using Scootly.Domain.Geo;

namespace Scootly.Api.Validators;

public readonly record struct ValidationResult(bool IsValid, string? Error)
{
    public static ValidationResult Valid => new(true, null);

    public static ValidationResult Invalid(string error) => new(false, error);
}

internal static class Coordinates
{
    public static bool IsValidLatitude(double value) => double.IsFinite(value) && value is >= -90 and <= 90;

    public static bool IsValidLongitude(double value) => double.IsFinite(value) && value is >= -180 and <= 180;
}

public sealed class CredentialsValidator
{
    public const int EmailMaxLength = 256;
    public const int PasswordMaxLength = 128;

    public ValidationResult Validate(string? email, string? password)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > EmailMaxLength || !MailAddress.TryCreate(email, out _))
            return ValidationResult.Invalid("Geçerli bir e-posta adresi girin.");

        if (string.IsNullOrEmpty(password) || password.Length > PasswordMaxLength)
            return ValidationResult.Invalid($"Parola 1-{PasswordMaxLength} karakter olmalı.");

        return ValidationResult.Valid;
    }
}

public sealed class StartRideRequestValidator
{
    public ValidationResult Validate(StartRideRequest request)
    {
        if (request.VehicleId == Guid.Empty)
            return ValidationResult.Invalid("VehicleId boş olamaz.");

        return ValidationResult.Valid;
    }
}

public sealed class CompleteRideRequestValidator
{
    public ValidationResult Validate(CompleteRideRequest request)
    {
        if (!Coordinates.IsValidLatitude(request.EndLatitude))
            return ValidationResult.Invalid("EndLatitude -90 ile 90 arasında olmalı.");

        if (!Coordinates.IsValidLongitude(request.EndLongitude))
            return ValidationResult.Invalid("EndLongitude -180 ile 180 arasında olmalı.");

        return ValidationResult.Valid;
    }
}

public sealed class RegisterVehicleRequestValidator
{
    public ValidationResult Validate(RegisterVehicleRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Brand) || request.Brand.Length > VehicleModel.BrandMaxLength)
            return ValidationResult.Invalid($"Marka 1-{VehicleModel.BrandMaxLength} karakter olmalı.");

        if (request.RangeKm is <= 0 or > VehicleModel.MaxRangeKm)
            return ValidationResult.Invalid($"Menzil 1-{VehicleModel.MaxRangeKm} km arasında olmalı.");

        if (!Coordinates.IsValidLatitude(request.Latitude) || !Coordinates.IsValidLongitude(request.Longitude))
            return ValidationResult.Invalid("Konum geçerli bir enlem/boylam olmalı.");

        if (request.BatteryPercentage is < 0 or > 100)
            return ValidationResult.Invalid("Batarya yüzdesi 0-100 arasında olmalı.");

        return ValidationResult.Valid;
    }
}

public sealed record VehicleQuery(
    double? MinLatitude,
    double? MaxLatitude,
    double? MinLongitude,
    double? MaxLongitude,
    bool OnlyAvailable,
    int PageNumber,
    int PageSize);

public sealed class VehicleQueryValidator
{
    public const int MaxPageSize = 100;

    /// <summary>Çok derin sayfalar hem pahalıdır hem de taşmaya yol açabilir.</summary>
    public const int MaxPageNumber = 10_000;

    public ValidationResult Validate(VehicleQuery query)
    {
        if (query.PageNumber is < 1 or > MaxPageNumber)
            return ValidationResult.Invalid($"pageNumber 1-{MaxPageNumber} arasında olmalı.");

        if (query.PageSize is < 1 or > MaxPageSize)
            return ValidationResult.Invalid($"pageSize 1-{MaxPageSize} arasında olmalı.");

        if (query.MinLatitude is { } minLat && !Coordinates.IsValidLatitude(minLat)
            || query.MaxLatitude is { } maxLat && !Coordinates.IsValidLatitude(maxLat))
            return ValidationResult.Invalid("Enlem filtreleri -90 ile 90 arasında olmalı.");

        if (query.MinLongitude is { } minLon && !Coordinates.IsValidLongitude(minLon)
            || query.MaxLongitude is { } maxLon && !Coordinates.IsValidLongitude(maxLon))
            return ValidationResult.Invalid("Boylam filtreleri -180 ile 180 arasında olmalı.");

        if (query.MinLatitude > query.MaxLatitude || query.MinLongitude > query.MaxLongitude)
            return ValidationResult.Invalid("Alt sınır üst sınırdan büyük olamaz.");

        return ValidationResult.Valid;
    }
}

public sealed class TelemetryBatchRequestValidator
{
    public const int MaxReadingsPerBatch = 500;

    /// <summary>Cihaz saatinin sunucudan bu kadar ileride olmasına izin verilir.</summary>
    public static readonly TimeSpan MaxClockSkew = TimeSpan.FromMinutes(5);

    /// <summary>Bundan eski okumalar (ör. uzun süre çevrimdışı kalmış cihaz) kabul edilmez.</summary>
    public static readonly TimeSpan MaxReadingAge = TimeSpan.FromHours(24);

    private readonly IClock _clock;

    public TelemetryBatchRequestValidator(IClock clock)
    {
        _clock = clock;
    }

    public ValidationResult Validate(TelemetryBatchRequest request)
    {
        if (request.Readings is null || request.Readings.Count == 0)
            return ValidationResult.Invalid("En az bir okuma gönderilmeli.");

        if (request.Readings.Count > MaxReadingsPerBatch)
            return ValidationResult.Invalid($"Bir partide en fazla {MaxReadingsPerBatch} okuma gönderilebilir.");

        var now = _clock.UtcNow;

        for (var i = 0; i < request.Readings.Count; i++)
        {
            var reading = request.Readings[i];

            if (reading is null || reading.VehicleId == Guid.Empty)
                return ValidationResult.Invalid($"Okuma {i}: VehicleId boş olamaz.");

            if (!Coordinates.IsValidLatitude(reading.Latitude) || !Coordinates.IsValidLongitude(reading.Longitude))
                return ValidationResult.Invalid($"Okuma {i}: konum geçerli bir enlem/boylam olmalı.");

            if (reading.BatteryPercentage is < 0 or > 100)
                return ValidationResult.Invalid($"Okuma {i}: batarya yüzdesi 0-100 arasında olmalı.");

            if (reading.RecordedAt is { } recordedAt)
            {
                var recordedAtUtc = recordedAt.Kind == DateTimeKind.Local ? recordedAt.ToUniversalTime() : recordedAt;

                if (recordedAtUtc > now + MaxClockSkew || recordedAtUtc < now - MaxReadingAge)
                    return ValidationResult.Invalid($"Okuma {i}: ölçüm zamanı kabul edilebilir aralığın dışında.");
            }
        }

        return ValidationResult.Valid;
    }
}

public sealed class CreateServiceAreaRequestValidator
{
    public ValidationResult Validate(CreateServiceAreaRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > ServiceArea.NameMaxLength)
            return ValidationResult.Invalid($"Bölge adı 1-{ServiceArea.NameMaxLength} karakter olmalı.");

        if (request.Boundary is null || request.Boundary.Count is < 3 or > ServiceArea.MaxBoundaryPoints)
            return ValidationResult.Invalid($"Sınır 3-{ServiceArea.MaxBoundaryPoints} noktadan oluşmalı.");

        if (request.Boundary.Any(p => p is null || !Coordinates.IsValidLatitude(p.Latitude) || !Coordinates.IsValidLongitude(p.Longitude)))
            return ValidationResult.Invalid("Sınır noktaları geçerli enlem/boylam olmalı.");

        return ValidationResult.Valid;
    }
}
