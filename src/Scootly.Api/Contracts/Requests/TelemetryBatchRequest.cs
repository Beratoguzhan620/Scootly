namespace Scootly.Api.Contracts.Requests;

public sealed record TelemetryReadingItem(Guid VehicleId, double Latitude, double Longitude, int BatteryPercentage);

public sealed record TelemetryBatchRequest(IReadOnlyList<TelemetryReadingItem> Readings);