namespace Scootly.Application.Billing.Commands;

public sealed record SettleRidePaymentCommand(Guid RideId, bool Success, string? FailureReason);
