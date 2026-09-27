namespace Scootly.Application.IntegrationEvents;

public sealed record VehicleBatteryLowIntegrationEvent(Guid VehicleId, int BatteryPercentage);
