namespace Scootly.Application.IntegrationEvents;

/// <summary>Bir sürüşün ödemesi için yetkilendirme sonucu (63. gün).</summary>
/// <remarks>
/// Saga'nın 3. adımı (69. gün). İki kaynaktan gelebilir: Worker'daki ödeme
/// tüketicisi (bugün sahte ödeme sağlayıcısını çağırıyor) ya da ödeme
/// sağlayıcısının webhook'u (70. gün). İkisi de aynı olayı üretiyor, dolayısıyla
/// sonucu işleyen tüketici kaynağı bilmek zorunda değil.
/// <para>
/// <c>Success = false</c> bir HATA değil, bir SONUÇ: kart reddedildi. Tüketici
/// bunu başarıyla işler (telafi yolu), yeniden denemez.
/// </para>
/// </remarks>
public sealed record PaymentAuthorizedIntegrationEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    Guid RideId,
    decimal Amount,
    bool Success,
    string? FailureReason = null) : IIntegrationEvent, IHasEventName
{
    public static string EventName => "payment.authorized";

    public void Validate()
    {
        if (EventId == Guid.Empty) throw new InvalidIntegrationEventException("EventId bos.");
        if (RideId == Guid.Empty) throw new InvalidIntegrationEventException("RideId bos.");
        if (Amount < 0) throw new InvalidIntegrationEventException("Amount negatif.");
    }
}
