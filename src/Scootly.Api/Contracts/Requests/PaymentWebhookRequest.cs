namespace Scootly.Api.Contracts.Requests;

public sealed record PaymentWebhookRequest(Guid RideId, bool Success, string Message, string Signature);