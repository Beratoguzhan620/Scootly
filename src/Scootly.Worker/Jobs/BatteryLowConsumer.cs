using System.Text.Json;
using Microsoft.Extensions.Options;
using Scootly.Application.IntegrationEvents;
using Scootly.Infrastructure.Messaging;
using Scootly.Infrastructure.Messaging.Consumers;

namespace Scootly.Worker.Jobs;

/// <summary>
/// Batarya düşük olaylarını saha operasyonlarına iletir. FieldOps bağlamı (saha görevi aggregate'i) henüz
/// yazılmadığı için görev şimdilik yapılandırılmış log olarak üretilir (bkz. teknik borç listesi).
/// </summary>
public sealed class BatteryLowConsumer : RabbitMqConsumerService
{
    public BatteryLowConsumer(
        RabbitMqConnectionProvider connectionProvider,
        IServiceScopeFactory scopeFactory,
        IOptions<MessagingOptions> options,
        ILogger<BatteryLowConsumer> logger)
        : base(connectionProvider, scopeFactory, options, logger)
    {
    }

    protected override string QueueName => "scootly.fieldops.battery-low";

    protected override IReadOnlyCollection<string> RoutingKeys => [IntegrationEventNames.VehicleBatteryLow];

    protected override Task<ConsumeResult> HandleAsync(ReceivedMessage message, IServiceProvider services, CancellationToken cancellationToken)
    {
        VehicleBatteryLowIntegrationEvent? integrationEvent;

        try
        {
            integrationEvent = JsonSerializer.Deserialize<VehicleBatteryLowIntegrationEvent>(message.Body);
        }
        catch (JsonException)
        {
            integrationEvent = null;
        }

        if (integrationEvent is null || integrationEvent.VehicleId == Guid.Empty)
            return Task.FromResult(ConsumeResult.DeadLetter);

        Logger.LogWarning(
            "Saha görevi: batarya değişimi gerekli — Vehicle={VehicleId}, %{BatteryPercentage}",
            integrationEvent.VehicleId, integrationEvent.BatteryPercentage);

        return Task.FromResult(ConsumeResult.Ack);
    }
}
