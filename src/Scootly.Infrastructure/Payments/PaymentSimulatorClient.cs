using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Polly.CircuitBreaker;
using Polly.Timeout;
using Scootly.Application.Abstractions;

namespace Scootly.Infrastructure.Payments;

public sealed class PaymentSimulatorClient : IPaymentGateway
{
    public const string IdempotencyKeyHeader = "Idempotency-Key";

    private readonly HttpClient _httpClient;
    private readonly ILogger<PaymentSimulatorClient> _logger;

    public PaymentSimulatorClient(HttpClient httpClient, ILogger<PaymentSimulatorClient> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task<PaymentGatewayResult> AuthorizeAsync(PaymentAuthorizationRequest request, CancellationToken cancellationToken = default)
    {
        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "/api/payments/authorize")
            {
                Content = JsonContent.Create(new { request.RideId, request.Amount })
            };
            httpRequest.Headers.Add(IdempotencyKeyHeader, request.IdempotencyKey);

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

            if (response.StatusCode == HttpStatusCode.PaymentRequired || response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadFromJsonAsync<AuthorizeResponseDto>(cancellationToken);

                if (body is null)
                    return new PaymentGatewayResult(PaymentGatewayOutcome.Unavailable, "Ödeme servisi yanıtı ayrıştırılamadı.");

                return body.Success
                    ? new PaymentGatewayResult(PaymentGatewayOutcome.Approved, body.Message)
                    : new PaymentGatewayResult(PaymentGatewayOutcome.Declined, body.Message);
            }

            if ((int)response.StatusCode is >= 400 and < 500)
            {
                // Hatalı istek: yeniden denemek aynı sonucu verir.
                return new PaymentGatewayResult(PaymentGatewayOutcome.Declined, $"Ödeme isteği reddedildi ({(int)response.StatusCode}).");
            }

            return new PaymentGatewayResult(PaymentGatewayOutcome.Unavailable, $"Ödeme servisi hatası ({(int)response.StatusCode}).");
        }
        catch (Exception ex) when (ex is HttpRequestException or BrokenCircuitException or TimeoutRejectedException or TaskCanceledException
                                   && !cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Ödeme servisine ulaşılamadı: Ride={RideId}", request.RideId);
            return new PaymentGatewayResult(PaymentGatewayOutcome.Unavailable, "Ödeme servisine şu an ulaşılamıyor.");
        }
    }

    private sealed record AuthorizeResponseDto(Guid RideId, bool Success, string Message);
}
