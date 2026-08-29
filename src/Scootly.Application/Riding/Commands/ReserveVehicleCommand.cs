namespace Scootly.Application.Riding.Commands;

public sealed record ReserveVehicleCommand(Guid VehicleId, Guid DriverId);