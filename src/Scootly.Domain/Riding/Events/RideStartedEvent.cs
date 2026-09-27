using Scootly.Domain.Common;

namespace Scootly.Domain.Riding.Events;

public sealed class RideStartedEvent : IDomainEvent
{
    public RideId RideId { get; }
    public Guid DriverId { get; }
    public Guid VehicleId { get; }
    public DateTime OccurredOn { get; }

    public RideStartedEvent(RideId rideId, Guid driverId, Guid vehicleId, DateTime occurredOn)
    {
        RideId = rideId;
        DriverId = driverId;
        VehicleId = vehicleId;
        OccurredOn = occurredOn;
    }
}
