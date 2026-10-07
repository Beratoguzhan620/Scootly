using System.Text.Json;
using Microsoft.Extensions.Options;
using Scootly.Application.FieldOps.Commands;
using Scootly.Application.IntegrationEvents;
using Scootly.Domain.FieldOps;
using Scootly.Infrastructure.Messaging;
using Scootly.Infrastructure.Messaging.Consumers;

namespace Scootly.Worker.Jobs;

/// <summary>Batarya düşük olaylarını, saha operasyonları için bir FieldTask'a dönüştürür.</summary>
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
        => ProcessAsync(message, services, Logger, cancellationToken);

    /// <summary>Mesaj işleme mantığı; broker olmadan test edilebilsin diye tüketici altyapısından ayrıdır.</summary>
    internal static async Task<ConsumeResult> ProcessAsync(ReceivedMessage message, IServiceProvider services, ILogger logger, CancellationToken cancellationToken)
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
            return ConsumeResult.DeadLetter;

        var handler = services.GetRequiredService<FieldTaskCommandHandler>();

        var result = await handler.Handle(
            new CreateFieldTaskCommand(integrationEvent.VehicleId, FieldTaskType.BatteryReplacement),
            cancellationToken);

        if (!result.IsSuccess)
        {
            logger.LogError(
                "Saha görevi oluşturulamadı: Vehicle={VehicleId}, Hata={Error}",
                integrationEvent.VehicleId, result.Error);

            return ConsumeResult.DeadLetter;
        }

        if (result.Value is { } fieldTaskId)
        {
            logger.LogInformation(
                "Saha görevi (batarya değişimi) açıldı: Task={FieldTaskId}, Vehicle={VehicleId}, %{BatteryPercentage}",
                fieldTaskId, integrationEvent.VehicleId, integrationEvent.BatteryPercentage);
        }
        else
        {
            logger.LogInformation("Araç için zaten açık bir batarya görevi var: Vehicle={VehicleId}", integrationEvent.VehicleId);
        }

        return ConsumeResult.Ack;
    }
}
