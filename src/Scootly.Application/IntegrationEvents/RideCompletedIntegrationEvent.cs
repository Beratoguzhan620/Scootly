namespace Scootly.Application.IntegrationEvents;

public sealed record RideCompletedIntegrationEvent(
    Guid RideId,
    Guid DriverId,
    Guid VehicleId,
    int DurationMinutes,
    double DistanceMeters);