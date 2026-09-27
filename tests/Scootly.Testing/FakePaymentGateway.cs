using System.Collections.Concurrent;
using Scootly.Application.Abstractions;

namespace Scootly.Testing;

/// <summary>Sonucu testten ayarlanabilen, çağrıları kaydeden ödeme sağlayıcısı.</summary>
public sealed class FakePaymentGateway : IPaymentGateway
{
    public PaymentGatewayOutcome NextOutcome { get; set; } = PaymentGatewayOutcome.Approved;

    public ConcurrentQueue<PaymentAuthorizationRequest> Requests { get; } = new();

    public Task<PaymentGatewayResult> AuthorizeAsync(PaymentAuthorizationRequest request, CancellationToken cancellationToken = default)
    {
        Requests.Enqueue(request);
        return Task.FromResult(new PaymentGatewayResult(NextOutcome, $"Sahte sonuç: {NextOutcome}"));
    }
}
