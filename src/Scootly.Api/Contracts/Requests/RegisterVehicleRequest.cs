namespace Scootly.Api.Contracts.Requests;

public sealed record RegisterVehicleRequest(
    string Brand,
    int RangeKm,
    double Latitude,
    double Longitude,
    int BatteryPercentage);