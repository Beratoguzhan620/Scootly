using System.Net;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Scootly.Api.Controllers;
using Scootly.Domain.Geo;
using Scootly.Domain.Riding;
using Scootly.Infrastructure.Payments;
using Scootly.Testing;
using Xunit;

namespace Scootly.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class WebhookTests
{
    private readonly ScootlyApiFactory _factory;

    public WebhookTests(ScootlyApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<Guid> SeedRideAwaitingPaymentAsync()
    {
        var vehicle = await _factory.SeedVehicleAsync();
        var ride = new Ride(RideId.New(), Guid.NewGuid(), vehicle.Id, new GeoPoint(41, 29), DateTime.UtcNow.AddMinutes(-10));
        ride.Complete(new GeoPoint(41.01, 29.01), DateTime.UtcNow, Tariff.Standard);

        await _factory.WithDbContextAsync(async db =>
        {
            db.Rides.Add(ride);
            await db.SaveChangesAsync();
        });

        return ride.Id;
    }

    private static HttpRequestMessage SignedRequest(string body, string secret, DateTimeOffset? timestamp = null)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/webhooks/payment-callback")
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };

        var unix = (timestamp ?? DateTimeOffset.UtcNow).ToUnixTimeSeconds();
        request.Headers.Add(PaymentWebhookValidator.SignatureHeaderName, PaymentWebhookValidator.CreateHeaderValue(secret, unix, body));

        return request;
    }

    private static string Body(Guid eventId, Guid rideId, bool success) => JsonSerializer.Serialize(new
    {
        eventId,
        rideId,
        success,
        amount = 25.00m,
        message = success ? "Ödeme onaylandı." : "Ödeme reddedildi.",
        idempotencyKey = $"ride-{rideId:N}-attempt-1"
    });

    [Fact]
    public async Task Gecerli_Imzali_Webhook_Odemeyi_Tamamlamali_Ve_Tekrari_Etkisiz_Olmali()
    {
        var rideId = await SeedRideAwaitingPaymentAsync();
        var eventId = Guid.NewGuid();
        var body = Body(eventId, rideId, success: true);
        var client = _factory.CreateClient();

        var first = await client.SendAsync(SignedRequest(body, TestSecrets.WebhookSecret));
        var replay = await client.SendAsync(SignedRequest(body, TestSecrets.WebhookSecret));

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);

        var ride = await _factory.WithDbContextAsync(db => db.Rides.AsNoTracking().SingleAsync(r => r.Id == rideId));
        Assert.Equal(PaymentStatus.Paid, ride.PaymentStatus);

        var processed = await _factory.WithDbContextAsync(db => db.ProcessedMessages
            .CountAsync(m => m.MessageId == eventId && m.Consumer == WebhooksController.PaymentWebhookConsumer));
        Assert.Equal(1, processed);
    }

    [Fact]
    public async Task Yanlis_Anahtarla_Imzalanmis_Webhook_Reddedilmeli()
    {
        var rideId = await SeedRideAwaitingPaymentAsync();
        var body = Body(Guid.NewGuid(), rideId, success: true);

        var response = await _factory.CreateClient().SendAsync(SignedRequest(body, "baska-bir-anahtar-baska-bir-anahtar-123456"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Imzadan_Sonra_Degistirilen_Govde_Reddedilmeli()
    {
        var rideId = await SeedRideAwaitingPaymentAsync();
        var signedBody = Body(Guid.NewGuid(), rideId, success: false);
        var request = SignedRequest(signedBody, TestSecrets.WebhookSecret);

        // Saldırgan imzayı koruyup "success" alanını değiştiriyor.
        request.Content = new StringContent(signedBody.Replace("\"success\":false", "\"success\":true"), Encoding.UTF8, "application/json");

        var response = await _factory.CreateClient().SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Suresi_Gecmis_Imza_Tekrar_Oynatilamaz()
    {
        var rideId = await SeedRideAwaitingPaymentAsync();
        var body = Body(Guid.NewGuid(), rideId, success: true);

        var response = await _factory.CreateClient().SendAsync(
            SignedRequest(body, TestSecrets.WebhookSecret, DateTimeOffset.UtcNow.AddHours(-1)));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Imzasiz_Webhook_Reddedilmeli()
    {
        var response = await _factory.CreateClient().PostAsync(
            "/api/webhooks/payment-callback",
            new StringContent(Body(Guid.NewGuid(), Guid.NewGuid(), true), Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
