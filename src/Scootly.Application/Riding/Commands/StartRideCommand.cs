namespace Scootly.Application.Riding.Commands;

public sealed class StartRideCommand
{
    public Guid VehicleId { get; }
    public Guid DriverId { get; }

    public StartRideCommand(Guid vehicleId, Guid driverId)
    {
        VehicleId = vehicleId;
        DriverId = driverId;
    }
}