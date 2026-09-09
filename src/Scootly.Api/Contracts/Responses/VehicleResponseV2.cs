namespace Scootly.Api.Contracts.Responses;

public sealed record VehicleResponseV2(
    Guid Id,
    double Latitude,
    double Longitude,
    int BatteryPercentage,
    string Status,
    string Brand,
    int RangeKm);