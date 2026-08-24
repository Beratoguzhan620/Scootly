using Scootly.Domain.Common;

namespace Scootly.Domain.Fleet.Events;

public sealed class VehicleRegisteredEvent : IDomainEvent
{
    public VehicleId VehicleId { get; }
    public DateTime OccurredOn { get; }

    public VehicleRegisteredEvent(VehicleId vehicleId, DateTime occurredOn)
    {
        VehicleId = vehicleId;
        OccurredOn = occurredOn;
    }
}