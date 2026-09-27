namespace Scootly.Api.Contracts.Requests;

public sealed record RegisterRequest(string Email, string Password);

public sealed record LoginRequest(string Email, string Password);

public sealed record DeviceTokenRequest(string ClientId, string ClientSecret);

public sealed record RegisterVehicleRequest(
    string Brand,
    int RangeKm,
    double Latitude,
    double Longitude,
    int BatteryPercentage);

public sealed record StartRideRequest(Guid VehicleId);

public sealed record CompleteRideRequest(double EndLatitude, double EndLongitude);

/// <param name="RecordedAt">Cihazın ölçüm zamanı (UTC). Gönderilmezse sunucunun alış zamanı kullanılır.</param>
public sealed record TelemetryReadingItem(
    Guid VehicleId,
    double Latitude,
    double Longitude,
    int BatteryPercentage,
    DateTime? RecordedAt = null);

public sealed record TelemetryBatchRequest(IReadOnlyList<TelemetryReadingItem> Readings);

/// <summary>Ödeme sağlayıcısının webhook gövdesi. İmza, bu gövdenin ham hali üzerinden doğrulanır.</summary>
public sealed record PaymentWebhookRequest(
    Guid EventId,
    Guid RideId,
    bool Success,
    decimal Amount,
    string? Message,
    string? IdempotencyKey);

public sealed record BoundaryPointRequest(double Latitude, double Longitude);

public sealed record CreateServiceAreaRequest(string Name, IReadOnlyList<BoundaryPointRequest> Boundary);
