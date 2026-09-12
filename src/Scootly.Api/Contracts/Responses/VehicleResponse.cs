namespace Scootly.Api.Contracts.Responses;

public sealed record VehicleResponse(
    Guid Id,
    double Latitude,
    double Longitude,
    int BatteryPercentage,
    string Status);