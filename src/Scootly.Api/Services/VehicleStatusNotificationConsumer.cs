using System.Text.Json;
using Microsoft.Extensions.Options;
using Scootly.Application.Abstractions;
using Scootly.Application.IntegrationEvents;
using Scootly.Infrastructure.Messaging;
using Scootly.Infrastructure.Messaging.Consumers;

namespace Scootly.Api.Services;

/// <summary>
/// Araç durum değişikliklerini (API'den, Worker'dan veya telemetriden kaynaklansın) SignalR üzerinden
/// aracın bulunduğu hizmet bölgesine iletir. Her API süreci kendi geçici kuyruğunu dinler (fan-out),
/// böylece hangi instance'a bağlı olursa olsun her istemci bildirimi alır.
/// </summary>
public sealed class VehicleStatusNotificationConsumer : RabbitMqConsumerService
{
    public VehicleStatusNotificationConsumer(
        RabbitMqConnectionProvider connectionProvider,
        IServiceScopeFactory scopeFactory,
        IOptions<MessagingOptions> options,
        ILogger<VehicleStatusNotificationConsumer> logger)
        : base(connectionProvider, scopeFactory, options, logger)
    {
    }

    protected override string QueueName => "scootly.api.vehicle-status-notifications";

    protected override IReadOnlyCollection<string> RoutingKeys => [IntegrationEventNames.VehicleStatusChanged];

    protected override bool IsDurable => false;

    protected override async Task<ConsumeResult> HandleAsync(ReceivedMessage message, IServiceProvider services, CancellationToken cancellationToken)
    {
        VehicleStatusChangedIntegrationEvent? integrationEvent;

        try
        {
            integrationEvent = JsonSerializer.Deserialize<VehicleStatusChangedIntegrationEvent>(message.Body);
        }
        catch (JsonException)
        {
            integrationEvent = null;
        }

        if (integrationEvent is null)
            return ConsumeResult.DeadLetter;

        var regionResolver = services.GetRequiredService<IRegionResolver>();
        var notifier = services.GetRequiredService<IFleetNotifier>();

        var region = await regionResolver.ResolveRegionAsync(integrationEvent.Latitude, integrationEvent.Longitude, cancellationToken);

        await notifier.NotifyVehicleStatusChangedAsync(integrationEvent.VehicleId, region, integrationEvent.NewStatus, cancellationToken);

        return ConsumeResult.Ack;
    }
}
