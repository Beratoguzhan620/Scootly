namespace Scootly.Application.IntegrationEvents;

public sealed record VehicleStatusChangedIntegrationEvent(
    Guid VehicleId,
    string OldStatus,
    string NewStatus,
    double Latitude,
    double Longitude);
