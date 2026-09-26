using System.Net.Http.Json;

namespace Scootly.Infrastructure.Payments;

public sealed class PaymentSimulatorClient
{
    private readonly HttpClient _httpClient;

    public PaymentSimulatorClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<PaymentAuthorizationResult> AuthorizeAsync(Guid rideId, decimal amount, CancellationToken cancellationToken = default)
    {
        try
        {
            var response = await _httpClient.PostAsJsonAsync(
                "/api/payments/authorize",
                new { RideId = rideId, Amount = amount },
                cancellationToken);

            var body = await response.Content.ReadFromJsonAsync<AuthorizeResponseDto>(cancellationToken: cancellationToken);

            return new PaymentAuthorizationResult(
                body?.Success ?? false,
                body?.Message ?? "Yanıt ayrıştırılamadı.");
        }
        catch (Exception ex)
        {
            return new PaymentAuthorizationResult(false, $"Ödeme servisi hatası: {ex.Message}");
        }
    }

    private sealed record AuthorizeResponseDto(Guid RideId, bool Success, string Message);
}

public sealed record PaymentAuthorizationResult(bool Success, string Message);