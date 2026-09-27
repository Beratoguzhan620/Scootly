using System.Text.Json;
using Microsoft.Extensions.Options;
using Scootly.Application.IntegrationEvents;
using Scootly.Application.Payments.Commands;
using Scootly.Domain.Common;
using Scootly.Infrastructure.Messaging;
using Scootly.Infrastructure.Messaging.Consumers;

namespace Scootly.Api.Services;

/// <summary>
/// Ödeme saga'sının tüketici adımı: tamamlanan veya terk edilen sürüşün ücretini tahsil eder.
/// Tahsilat doğası gereği idempotenttir (ödeme durumu kontrolü + sağlayıcı idempotency anahtarı);
/// ödeme servisine ulaşılamazsa mesaj retry kuyruğu üzerinden tekrar denenir.
/// </summary>
public sealed class RideChargeConsumer : RabbitMqConsumerService
{
    public const string Queue = "scootly.payments.ride-charges";

    public RideChargeConsumer(
        RabbitMqConnectionProvider connectionProvider,
        IServiceScopeFactory scopeFactory,
        IOptions<MessagingOptions> options,
        ILogger<RideChargeConsumer> logger)
        : base(connectionProvider, scopeFactory, options, logger)
    {
    }

    protected override string QueueName => Queue;

    protected override IReadOnlyCollection<string> RoutingKeys =>
        [IntegrationEventNames.RideCompleted, IntegrationEventNames.RideAbandoned];

    protected override async Task<ConsumeResult> HandleAsync(ReceivedMessage message, IServiceProvider services, CancellationToken cancellationToken)
    {
        var rideId = TryReadRideId(message.Body);

        if (rideId is null)
        {
            Logger.LogError("Ödeme mesajı ayrıştırılamadı ({MessageId}).", message.MessageId);
            return ConsumeResult.DeadLetter;
        }

        var handler = services.GetRequiredService<ChargeRideCommandHandler>();
        var result = await handler.Handle(new ChargeRideCommand(rideId.Value), cancellationToken);

        if (!result.IsSuccess)
        {
            if (result.ErrorType == ErrorType.NotFound)
            {
                Logger.LogError("Ücretlendirilecek sürüş bulunamadı: {RideId}", rideId);
                return ConsumeResult.DeadLetter;
            }

            Logger.LogWarning("Sürüş ücretlendirilemedi ({RideId}): {Error}", rideId, result.Error);
            return ConsumeResult.Retry;
        }

        switch (result.Value)
        {
            case ChargeOutcome.Approved:
                Logger.LogInformation("Ödeme alındı: {RideId}", rideId);
                return ConsumeResult.Ack;

            case ChargeOutcome.Declined:
                // Ret bir deneme olarak kaydedildi; sonraki denemeleri Worker'daki bekleyen ödeme servisi yapar.
                Logger.LogWarning("Ödeme reddedildi, sürüş ödeme bekliyor: {RideId}", rideId);
                return ConsumeResult.Ack;

            case ChargeOutcome.GatewayUnavailable:
                return ConsumeResult.Retry;

            default:
                return ConsumeResult.Ack;
        }
    }

    private static Guid? TryReadRideId(string body)
    {
        try
        {
            using var document = JsonDocument.Parse(body);

            return document.RootElement.TryGetProperty(nameof(RideCompletedIntegrationEvent.RideId), out var property)
                   && property.TryGetGuid(out var rideId)
                   && rideId != Guid.Empty
                ? rideId
                : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
