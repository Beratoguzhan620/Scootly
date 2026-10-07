namespace Scootly.Api.Contracts.Responses;

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int PageNumber,
    int PageSize,
    int TotalCount)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}

public sealed record TokenResponse(string Token);

public sealed record VehicleResponse(
    Guid Id,
    double Latitude,
    double Longitude,
    int BatteryPercentage,
    string Status);

public sealed record VehicleResponseV2(
    Guid Id,
    double Latitude,
    double Longitude,
    int BatteryPercentage,
    string Status,
    string Brand,
    int RangeKm);

public sealed record RegisterVehicleResponse(Guid Id);

public sealed record RideResponse(
    Guid Id,
    Guid DriverId,
    Guid VehicleId,
    string Status,
    DateTime StartedAt,
    DateTime? EndedAt,
    decimal? Fare,
    string PaymentStatus);

public sealed record StartRideResponse(Guid RideId);

public sealed record CompleteRideResponse(Guid RideId, string Status, string PaymentStatus, decimal Fare);

public sealed record PaymentStatusResponse(Guid RideId, string PaymentStatus, decimal? Fare);

public sealed record TelemetryBatchResponse(int Accepted, IReadOnlyList<Guid> RejectedVehicleIds);

public sealed record ServiceAreaResponse(string Name, IReadOnlyList<BoundaryPointResponse> Boundary);

public sealed record BoundaryPointResponse(double Latitude, double Longitude);

public sealed record AccountResponse(Guid Id, string Email, IReadOnlyList<string> Roles);
