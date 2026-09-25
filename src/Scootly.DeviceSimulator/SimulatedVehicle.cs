namespace Scootly.DeviceSimulator;

public sealed class SimulatedVehicle
{
    public Guid VehicleId { get; }
    public double Latitude { get; private set; }
    public double Longitude { get; private set; }
    public int BatteryPercentage { get; private set; }

    private static readonly Random Random = new();

    public SimulatedVehicle(Guid vehicleId, double startLatitude, double startLongitude, int startBattery)
    {
        VehicleId = vehicleId;
        Latitude = startLatitude;
        Longitude = startLongitude;
        BatteryPercentage = startBattery;
    }

    public void Move()
    {
        Latitude += (Random.NextDouble() - 0.5) * 0.001;
        Longitude += (Random.NextDouble() - 0.5) * 0.001;
    }

    public void DrainBattery()
    {
        if (BatteryPercentage > 0)
            BatteryPercentage -= Random.Next(0, 2);
    }
}