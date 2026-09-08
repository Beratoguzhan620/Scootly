namespace Scootly.Api.Contracts.Requests;

public sealed record StartRideRequest(Guid VehicleId, Guid DriverId);