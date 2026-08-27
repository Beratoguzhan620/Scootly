namespace Scootly.Application.Riding.Commands;

public sealed class ReserveVehicleCommand
{
    public Guid VehicleId { get; }
    public Guid DriverId { get; }

    public ReserveVehicleCommand(Guid vehicleId, Guid driverId)
    {
        VehicleId = vehicleId;
        DriverId = driverId;
    }
}