namespace Scootly.Application.Abstractions;

/// <summary>Ödeme sağlayıcısı (69. gün; gerçek simülatör 71. günde).</summary>
/// <remarks>
/// <b>Tekrar anahtarı (idempotency key) zorunlu.</b> Tüketici ödemeyi isteyip
/// sonucu kaydedemeden çökerse mesaj yeniden gelir ve ödeme ikinci kez
/// istenir. Aynı anahtarla gelen ikinci isteği ilkinin sonucuyla cevaplamak
/// sağlayıcının işi; anahtar olmadan aynı sürüş için iki kez para çekilir.
/// Bu projede anahtar sürüş kimliği: bir sürüş için tek bir ödeme var.
/// </remarks>
public interface IPaymentGateway
{
    Task<PaymentAuthorizationResult> AuthorizeAsync(
        PaymentAuthorizationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record PaymentAuthorizationRequest(Guid IdempotencyKey, Guid DriverId, decimal Amount);

/// <param name="Approved">Sağlayıcı ödemeyi onayladı mı.</param>
/// <param name="DeclineReason">Reddedildiyse sebebi.</param>
public sealed record PaymentAuthorizationResult(bool Approved, string? DeclineReason);
