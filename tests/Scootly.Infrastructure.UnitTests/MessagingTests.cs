using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Scootly.Application.Abstractions;
using Scootly.Application.IntegrationEvents;
using Scootly.Infrastructure.Messaging;
using Scootly.Infrastructure.Payments;
using Xunit;

namespace Scootly.Infrastructure.UnitTests;

public sealed class MessagingTests
{
    [Fact]
    public void Deneme_Basligi_Yoksa_Ilk_Denemedir()
    {
        Assert.Equal(1, MessageHeaders.ReadAttempt(null));
        Assert.Equal(1, MessageHeaders.ReadAttempt(new Dictionary<string, object?>()));
    }

    [Fact]
    public void Deneme_Basligi_Farkli_Tiplerde_Okunur()
    {
        // RabbitMQ sayiyi int ya da long, metni byte[] olarak geri verebiliyor.
        Assert.Equal(2, MessageHeaders.ReadAttempt(new Dictionary<string, object?> { [MessageHeaders.Attempt] = 2 }));
        Assert.Equal(3, MessageHeaders.ReadAttempt(new Dictionary<string, object?> { [MessageHeaders.Attempt] = 3L }));
        Assert.Equal(4, MessageHeaders.ReadAttempt(new Dictionary<string, object?> { [MessageHeaders.Attempt] = Encoding.UTF8.GetBytes("4") }));
        Assert.Equal(1, MessageHeaders.ReadAttempt(new Dictionary<string, object?> { [MessageHeaders.Attempt] = 0 }));
    }

    [Fact]
    public void Eksik_Alanli_Mesaj_Okunamaz()
    {
        // RespectRequiredConstructorParameters: eksik alan Guid.Empty olmuyor, hata veriyor.
        Assert.Throws<System.Text.Json.JsonException>(() =>
            IntegrationEventSerializer.Deserialize<RideCompletedIntegrationEvent>(Encoding.UTF8.GetBytes("""{"rideId":"bozuk"}""")));
    }

    [Fact]
    public void Olay_Yazilip_Okundugunda_Ayni_Kalir()
    {
        var olay = new PaymentAuthorizedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), 12.5m, false, "Kart reddedildi.");

        var geri = IntegrationEventSerializer.Deserialize<PaymentAuthorizedIntegrationEvent>(IntegrationEventSerializer.Serialize(olay));

        Assert.Equal(olay, geri);
    }

    [Fact]
    public void Topolojide_Her_Kuyruk_Ve_Olay_Bir_Kez_Gecer()
    {
        var adlar = RabbitMqTopology.Queues.Select(q => q.Name).ToList();
        Assert.Equal(adlar.Count, adlar.Distinct(StringComparer.Ordinal).Count());
        Assert.Contains(RabbitMqTopology.Queues, q => q.RoutingKey == PaymentAuthorizedIntegrationEvent.EventName);
        Assert.Contains(RabbitMqTopology.Queues, q => q.RoutingKey == PaymentAuthorizationRequestedIntegrationEvent.EventName);
    }

    [Fact]
    public async Task Sahte_Saglayici_Ayni_Anahtara_Ayni_Karari_Verir()
    {
        var odeme = new FakePaymentGateway(new PaymentOptions { FakeDeclineAbove = 50m }, NullLogger<FakePaymentGateway>.Instance);
        var anahtar = Guid.NewGuid();

        var ilk = await odeme.AuthorizeAsync(new PaymentAuthorizationRequest(anahtar, Guid.NewGuid(), 100m));
        var ikinci = await odeme.AuthorizeAsync(new PaymentAuthorizationRequest(anahtar, Guid.NewGuid(), 10m));

        Assert.False(ilk.Approved);
        Assert.Equal(ilk, ikinci);
    }
}
