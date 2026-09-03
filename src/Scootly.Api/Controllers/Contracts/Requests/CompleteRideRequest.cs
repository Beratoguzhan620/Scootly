namespace Scootly.Api.Contracts.Requests;

public sealed record CompleteRideRequest(Guid DriverId, double EndLatitude, double EndLongitude);