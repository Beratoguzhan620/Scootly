namespace Scootly.Application.Fleet.Commands;

public sealed record RegisterVehicleCommand(
    string Brand,
    int RangeKm,
    double Latitude,
    double Longitude,
    int BatteryPercentage);
