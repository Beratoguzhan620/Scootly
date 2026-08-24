using Scootly.Domain.Common;

namespace Scootly.Domain.Fleet.Events;

public sealed class VehicleStatusChangedEvent : IDomainEvent
{
    public VehicleId VehicleId { get; }
    public VehicleStatus OldStatus { get; }
    public VehicleStatus NewStatus { get; }
    public DateTime OccurredOn { get; }

    public VehicleStatusChangedEvent(
        VehicleId vehicleId, VehicleStatus oldStatus, VehicleStatus newStatus, DateTime occurredOn)
    {
        VehicleId = vehicleId;
        OldStatus = oldStatus;
        NewStatus = newStatus;
        OccurredOn = occurredOn;
    }
}