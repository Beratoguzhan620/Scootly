using Scootly.Application.FieldOps.Commands;
using Scootly.Application.IntegrationEvents;
using Scootly.Domain.Common;

namespace Scootly.Infrastructure.Messaging.Consumers;

/// <summary>Batarya düştü → saha görevi aç (64. gün).</summary>
public sealed class BatteryLowConsumer : IIntegrationEventConsumer<VehicleBatteryLowIntegrationEvent>
{
    private readonly OpenBatteryFieldTaskCommandHandler _handler;

    public BatteryLowConsumer(OpenBatteryFieldTaskCommandHandler handler)
    {
        _handler = handler;
    }

    public static QueueDefinition Queue => RabbitMqTopology.FieldTasks;

    public Task<Result> ConsumeAsync(VehicleBatteryLowIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return _handler.Handle(
            new OpenBatteryFieldTaskCommand(integrationEvent.VehicleId, integrationEvent.BatteryPercentage),
            cancellationToken);
    }
}
