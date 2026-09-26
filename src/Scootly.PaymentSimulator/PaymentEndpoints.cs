namespace Scootly.PaymentSimulator;

public static class PaymentEndpoints
{
    public static void MapPaymentEndpoints(this WebApplication app)
    {
        app.MapPost("/api/payments/authorize", async (AuthorizeRequest request) =>
        {
            if (FailureInjector.ShouldFail())
            {
                return Results.Json(new AuthorizeResponse(request.RideId, false, "Ödeme reddedildi."), statusCode: 402);
            }

            await Task.Delay(Random.Shared.Next(50, 300));

            return Results.Ok(new AuthorizeResponse(request.RideId, true, "Ödeme onaylandı."));
        });

        app.MapPost("/api/failure-rate", (SetFailureRateRequest request) =>
        {
            FailureInjector.SetFailureRate(request.Percent);
            return Results.Ok(new { Message = $"Hata oranı %{request.Percent} olarak ayarlandı." });
        });
    }
}

public sealed record AuthorizeRequest(Guid RideId, decimal Amount);
public sealed record AuthorizeResponse(Guid RideId, bool Success, string Message);
public sealed record SetFailureRateRequest(int Percent);