using System.Collections.Concurrent;

namespace Scootly.PaymentSimulator;

public static class PaymentEndpoints
{
    public const string IdempotencyKeyHeader = "Idempotency-Key";

    public static void MapPaymentEndpoints(this WebApplication app)
    {
        app.MapPost("/api/payments/authorize", AuthorizeAsync);

        // Hata enjeksiyonu yalnızca geliştirme ortamında açıktır; aksi halde herkes sağlayıcının davranışını değiştirebilirdi.
        if (app.Environment.IsDevelopment())
        {
            app.MapPost("/api/failure-rate", (SetFailureRateRequest request, FailureInjector injector) =>
            {
                injector.Configure(request.DeclinePercent, request.OutagePercent);

                return Results.Ok(new
                {
                    Message = $"Ret oranı %{injector.DeclineRatePercent}, kesinti oranı %{injector.OutageRatePercent} olarak ayarlandı."
                });
            });
        }
    }

    /// <summary>
    /// Aynı Idempotency-Key ile gelen tekrar istekler, ilk isteğin sonucunu döndürür (ikinci tahsilat oluşmaz).
    /// Bu, istemcinin zaman aşımı sonrası güvenle yeniden denemesini sağlar.
    /// </summary>
    private static async Task<IResult> AuthorizeAsync(
        HttpRequest httpRequest,
        AuthorizeRequest request,
        FailureInjector injector,
        IdempotencyStore store,
        WebhookSender webhookSender,
        ILoggerFactory loggerFactory,
        IHostApplicationLifetime lifetime)
    {
        var idempotencyKey = httpRequest.Headers[IdempotencyKeyHeader].ToString();

        if (string.IsNullOrWhiteSpace(idempotencyKey) || idempotencyKey.Length > 200)
            return Results.Problem("Idempotency-Key başlığı zorunludur (en fazla 200 karakter).", statusCode: StatusCodes.Status400BadRequest);

        if (request.RideId == Guid.Empty || request.Amount <= 0)
            return Results.Problem("RideId ve pozitif bir Amount zorunludur.", statusCode: StatusCodes.Status400BadRequest);

        if (store.TryGet(idempotencyKey, out var previous))
            return ToResult(previous);

        // Geçici kesinti: sonuç kaydedilmez, istemci aynı anahtarla yeniden deneyebilir.
        if (injector.ShouldSimulateOutage())
            return Results.Problem("Ödeme servisi geçici olarak kullanılamıyor.", statusCode: StatusCodes.Status503ServiceUnavailable);

        await Task.Delay(Random.Shared.Next(50, 300));

        var response = injector.ShouldDecline()
            ? new AuthorizeResponse(request.RideId, false, "Ödeme reddedildi.")
            : new AuthorizeResponse(request.RideId, true, "Ödeme onaylandı.");

        response = store.GetOrAdd(idempotencyKey, response);

        if (webhookSender.IsEnabled)
        {
            var logger = loggerFactory.CreateLogger("Webhook");

            // Webhook, yanıttan bağımsız olarak arka planda gönderilir (gerçek sağlayıcılarda olduğu gibi).
            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(500), lifetime.ApplicationStopping);
                    await webhookSender.SendAsync(response, request.Amount, idempotencyKey, lifetime.ApplicationStopping);
                }
                catch (OperationCanceledException)
                {
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Webhook gönderimi başarısız: {RideId}", response.RideId);
                }
            });
        }

        return ToResult(response);
    }

    private static IResult ToResult(AuthorizeResponse response)
        => response.Success ? Results.Ok(response) : Results.Json(response, statusCode: StatusCodes.Status402PaymentRequired);
}

/// <summary>Bellek içi idempotency kaydı (simülatör için yeterli; gerçek sağlayıcılar bunu kalıcı tutar).</summary>
public sealed class IdempotencyStore
{
    private readonly ConcurrentDictionary<string, AuthorizeResponse> _responses = new(StringComparer.Ordinal);

    public bool TryGet(string key, out AuthorizeResponse response) => _responses.TryGetValue(key, out response!);

    public AuthorizeResponse GetOrAdd(string key, AuthorizeResponse response) => _responses.GetOrAdd(key, response);
}

public sealed record AuthorizeRequest(Guid RideId, decimal Amount);

public sealed record AuthorizeResponse(Guid RideId, bool Success, string Message);

public sealed record SetFailureRateRequest(int DeclinePercent, int OutagePercent = 0);
