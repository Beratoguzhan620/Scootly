using System.Text.Json;
using Scootly.Application.IntegrationEvents;
using Scootly.Domain.Common;
using Scootly.Domain.Fleet.Events;
using Scootly.Domain.Riding.Events;
using Scootly.Infrastructure.Messaging.Outbox;

namespace Scootly.Infrastructure.Persistence;

/// <summary>
/// Domain olaylarını süreç dışına yayınlanacak entegrasyon olaylarına çevirir.
/// Yalnızca başka bir bileşenin tepki verdiği olaylar eşlenir; diğerleri (örn. VehicleRegistered) süreç içinde kalır.
/// </summary>
internal static class DomainEventOutboxMapper
{
    public static OutboxMessage? ToOutboxMessage(IDomainEvent domainEvent) => domainEvent switch
    {
        RideCompletedEvent e => Create(IntegrationEventNames.RideCompleted, e.OccurredOn, new RideCompletedIntegrationEvent(
            e.RideId.Value, e.DriverId, e.VehicleId, (int)Math.Ceiling(e.Duration.TotalMinutes), e.DistanceMeters, e.Fare)),

        RideAbandonedEvent e => Create(IntegrationEventNames.RideAbandoned, e.OccurredOn, new RideAbandonedIntegrationEvent(
            e.RideId.Value, e.DriverId, e.VehicleId, (int)Math.Ceiling(e.Duration.TotalMinutes), e.Fare)),

        VehicleBatteryLowEvent e => Create(IntegrationEventNames.VehicleBatteryLow, e.OccurredOn, new VehicleBatteryLowIntegrationEvent(
            e.VehicleId.Value, e.BatteryPercentage)),

        VehicleStatusChangedEvent e => Create(IntegrationEventNames.VehicleStatusChanged, e.OccurredOn, new VehicleStatusChangedIntegrationEvent(
            e.VehicleId.Value, e.OldStatus.ToString(), e.NewStatus.ToString(), e.Location.Latitude, e.Location.Longitude)),

        _ => null
    };

    private static OutboxMessage Create<T>(string eventType, DateTime occurredOn, T payload)
        => new(Guid.NewGuid(), eventType, JsonSerializer.Serialize(payload), occurredOn);
}
