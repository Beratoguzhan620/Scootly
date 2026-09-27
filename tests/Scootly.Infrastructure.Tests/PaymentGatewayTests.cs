using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Polly;
using Scootly.Application.Abstractions;
using Scootly.Infrastructure.Payments;
using Xunit;

namespace Scootly.Infrastructure.Tests;

public sealed class PaymentResiliencePoliciesTests
{
    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, true)]
    [InlineData(HttpStatusCode.ServiceUnavailable, true)]
    [InlineData(HttpStatusCode.RequestTimeout, true)]
    [InlineData(HttpStatusCode.TooManyRequests, true)]
    [InlineData(HttpStatusCode.PaymentRequired, false)]
    [InlineData(HttpStatusCode.BadRequest, false)]
    [InlineData(HttpStatusCode.OK, false)]
    public void Yalnizca_Gecici_Hatalar_Yeniden_Denenmeli(HttpStatusCode statusCode, bool expected)
    {
        var outcome = Outcome.FromResult(new HttpResponseMessage(statusCode));

        Assert.Equal(expected, PaymentResiliencePolicies.IsTransient(outcome));
    }

    [Fact]
    public void Ag_Hatasi_Gecici_Sayilmali()
    {
        Assert.True(PaymentResiliencePolicies.IsTransient(Outcome.FromException<HttpResponseMessage>(new HttpRequestException("ağ"))));
    }
}

public sealed class PaymentSimulatorClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _respond;

        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) => _respond = respond;

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(_respond(request));
        }
    }

    private static readonly PaymentAuthorizationRequest Request = new(Guid.NewGuid(), 25m, "ride-x-attempt-1");

    private static (PaymentSimulatorClient Client, StubHandler Handler) Create(HttpStatusCode status, object? body = null)
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(status)
        {
            Content = body is null ? new StringContent("") : JsonContent.Create(body)
        });

        var client = new PaymentSimulatorClient(new HttpClient(handler) { BaseAddress = new Uri("http://payments.test") }, NullLogger<PaymentSimulatorClient>.Instance);
        return (client, handler);
    }

    [Fact]
    public async Task Onay_Approved_Olarak_Donmeli_Ve_Idempotency_Anahtari_Gonderilmeli()
    {
        var (client, handler) = Create(HttpStatusCode.OK, new { Request.RideId, Success = true, Message = "ok" });

        var result = await client.AuthorizeAsync(Request);

        Assert.Equal(PaymentGatewayOutcome.Approved, result.Outcome);
        Assert.Equal(Request.IdempotencyKey, handler.LastRequest!.Headers.GetValues(PaymentSimulatorClient.IdempotencyKeyHeader).Single());
    }

    [Fact]
    public async Task Odeme_Reddi_Declined_Olarak_Donmeli()
    {
        var (client, _) = Create(HttpStatusCode.PaymentRequired, new { Request.RideId, Success = false, Message = "ret" });

        var result = await client.AuthorizeAsync(Request);

        Assert.Equal(PaymentGatewayOutcome.Declined, result.Outcome);
    }

    [Fact]
    public async Task Sunucu_Hatasi_Unavailable_Olarak_Donmeli()
    {
        var (client, _) = Create(HttpStatusCode.ServiceUnavailable);

        var result = await client.AuthorizeAsync(Request);

        Assert.Equal(PaymentGatewayOutcome.Unavailable, result.Outcome);
    }

    [Fact]
    public async Task Ag_Hatasi_Unavailable_Olarak_Donmeli()
    {
        var handler = new StubHandler(_ => throw new HttpRequestException("bağlantı reddedildi"));
        var client = new PaymentSimulatorClient(new HttpClient(handler) { BaseAddress = new Uri("http://payments.test") }, NullLogger<PaymentSimulatorClient>.Instance);

        var result = await client.AuthorizeAsync(Request);

        Assert.Equal(PaymentGatewayOutcome.Unavailable, result.Outcome);
    }
}
