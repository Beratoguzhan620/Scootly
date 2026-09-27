namespace Scootly.Application.IntegrationEvents;

public sealed record RideAbandonedIntegrationEvent(
    Guid RideId,
    Guid DriverId,
    Guid VehicleId,
    int DurationMinutes,
    decimal Fare);
