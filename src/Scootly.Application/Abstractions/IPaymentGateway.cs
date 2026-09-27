namespace Scootly.Application.Abstractions;

public interface IPaymentGateway
{
    Task<PaymentGatewayResult> AuthorizeAsync(PaymentAuthorizationRequest request, CancellationToken cancellationToken = default);
}

/// <param name="IdempotencyKey">Aynı anahtarla yapılan tekrar istekler sağlayıcıda ikinci kez tahsilat oluşturmaz.</param>
public sealed record PaymentAuthorizationRequest(Guid RideId, decimal Amount, string IdempotencyKey);

public enum PaymentGatewayOutcome
{
    Approved,

    /// <summary>Kalıcı iş reddi (örn. yetersiz bakiye). Yeniden denemek anlamsız; deneme sayılır.</summary>
    Declined,

    /// <summary>Geçici hata (ağ, zaman aşımı, devre açık). Deneme sayılmaz; daha sonra tekrar denenir.</summary>
    Unavailable
}

public sealed record PaymentGatewayResult(PaymentGatewayOutcome Outcome, string Message);
