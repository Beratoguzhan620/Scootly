namespace Scootly.Application.IntegrationEvents;

/// <summary>Ücreti hesaplanan sürüş için ödeme isteniyor (69. gün — saga'nın 2. adımı).</summary>
public sealed record PaymentAuthorizationRequestedIntegrationEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    Guid RideId,
    Guid DriverId,
    decimal Amount) : IIntegrationEvent, IHasEventName
{
    public static string EventName => "payment.authorization-requested";

    public void Validate()
    {
        if (EventId == Guid.Empty) throw new InvalidIntegrationEventException("EventId bos.");
        if (RideId == Guid.Empty) throw new InvalidIntegrationEventException("RideId bos.");
        if (DriverId == Guid.Empty) throw new InvalidIntegrationEventException("DriverId bos.");
        if (Amount < 0) throw new InvalidIntegrationEventException("Amount negatif.");
    }
}
