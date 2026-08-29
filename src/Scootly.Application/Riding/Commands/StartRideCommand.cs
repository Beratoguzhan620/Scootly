namespace Scootly.Application.Riding.Commands;

public sealed record StartRideCommand(Guid VehicleId, Guid DriverId);