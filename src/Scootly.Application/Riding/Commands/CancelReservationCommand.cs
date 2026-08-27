namespace Scootly.Application.Riding.Commands;

public sealed class CancelReservationCommand
{
    public Guid VehicleId { get; }

    public CancelReservationCommand(Guid vehicleId)
    {
        VehicleId = vehicleId;
    }
}