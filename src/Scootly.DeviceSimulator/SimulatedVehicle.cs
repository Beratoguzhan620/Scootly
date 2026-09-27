namespace Scootly.DeviceSimulator;

public sealed class SimulatedVehicle
{
    public Guid VehicleId { get; }
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public int BatteryPercentage { get; private set; }

    public SimulatedVehicle(Guid vehicleId, double startLatitude, double startLongitude, int startBattery)
    {
        VehicleId = vehicleId;
        Latitude = startLatitude;
        Longitude = startLongitude;
        BatteryPercentage = startBattery;
    }

    public void Move()
    {
        Latitude = Math.Clamp(Latitude + (Random.Shared.NextDouble() - 0.5) * 0.001, -90, 90);
        Longitude = Math.Clamp(Longitude + (Random.Shared.NextDouble() - 0.5) * 0.001, -180, 180);
    }

    public void DrainBattery()
    {
        if (BatteryPercentage > 0)
            BatteryPercentage -= Random.Shared.Next(0, 2);
    }
}
