namespace Scootly.Application.Billing.Commands;

public sealed record AuthorizeRidePaymentCommand(Guid RideId, Guid DriverId, decimal Amount);
