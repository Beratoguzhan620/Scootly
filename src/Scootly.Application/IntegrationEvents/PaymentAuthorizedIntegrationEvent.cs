namespace Scootly.Application.IntegrationEvents;

/// <summary>Bir sürüşün ödemesi için yetkilendirme sonucu (63. gün).</summary>
/// <remarks>
/// Bugün yayınlayan ya da dinleyen yok. Sözleşme şimdi tanımlanıyor çünkü
/// 69. günün saga'sı (sürüş bitti → ödeme → araç serbest) bu üç olay
/// üzerine kurulacak; sözleşmeyi ilk kullanıldığı gün değil, tasarlandığı gün
/// yazmak onu kullanım yerinin şekline uydurmayı engelliyor.
/// </remarks>
public sealed record PaymentAuthorizedIntegrationEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    Guid RideId,
    decimal Amount,
    bool Success) : IIntegrationEvent, IHasEventName
{
    public static string EventName => "payment.authorized";

    public void Validate()
    {
        if (EventId == Guid.Empty) throw new InvalidIntegrationEventException("EventId bos.");
        if (RideId == Guid.Empty) throw new InvalidIntegrationEventException("RideId bos.");
        if (Amount < 0) throw new InvalidIntegrationEventException("Amount negatif.");
    }
}
