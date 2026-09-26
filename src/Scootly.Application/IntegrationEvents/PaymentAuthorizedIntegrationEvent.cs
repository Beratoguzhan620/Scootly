namespace Scootly.Application.IntegrationEvents;

public sealed record PaymentAuthorizedIntegrationEvent(Guid RideId, decimal Amount, bool Success);