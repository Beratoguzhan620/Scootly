namespace Scootly.Application.FieldOps.Commands;

public sealed record OpenBatteryFieldTaskCommand(Guid VehicleId, int BatteryPercentage);
