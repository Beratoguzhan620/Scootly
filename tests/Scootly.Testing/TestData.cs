using Scootly.Domain.Fleet;
using Scootly.Domain.Geo;
using Scootly.Domain.Riding;

namespace Scootly.Testing;

public static class TestData
{
    public static Vehicle NewVehicle(
        double latitude = 41.0,
        double longitude = 29.0,
        int batteryPercentage = 80,
        string brand = "Xiaomi",
        DateTime? registeredAt = null)
        => new(
            VehicleId.New(),
            new VehicleModel(brand, 25),
            new GeoPoint(latitude, longitude),
            new BatteryLevel(batteryPercentage),
            registeredAt ?? DateTime.UtcNow);

    public static Ride NewActiveRide(Guid driverId, Guid vehicleId, DateTime? startedAt = null)
        => new(RideId.New(), driverId, vehicleId, new GeoPoint(41.0, 29.0), startedAt ?? DateTime.UtcNow);
}
