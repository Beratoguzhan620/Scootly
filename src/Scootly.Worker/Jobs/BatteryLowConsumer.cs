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

    protected override async Task<ConsumeResult> HandleAsync(ReceivedMessage message, IServiceProvider services, CancellationToken cancellationToken)
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
            Logger.LogError(
                "Saha görevi oluşturulamadı: Vehicle={VehicleId}, Hata={Error}",
                integrationEvent.VehicleId, result.Error);

            return ConsumeResult.DeadLetter;
        }

        Logger.LogInformation(
            "Saha görevi (batarya değişimi) işlendi: Vehicle={VehicleId}, %{BatteryPercentage}",
            integrationEvent.VehicleId, integrationEvent.BatteryPercentage);

        return ConsumeResult.Ack;
    }
}