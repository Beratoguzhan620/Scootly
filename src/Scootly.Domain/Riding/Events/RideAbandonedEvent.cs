using Scootly.Domain.Common;

namespace Scootly.Domain.Riding.Events;

public sealed class RideAbandonedEvent : IDomainEvent
{
    public RideId RideId { get; }
    public Guid DriverId { get; }
    public Guid VehicleId { get; }
    public TimeSpan Duration { get; }
    public decimal Fare { get; }
    public DateTime OccurredOn { get; }

    public RideAbandonedEvent(RideId rideId, Guid driverId, Guid vehicleId, TimeSpan duration, decimal fare, DateTime occurredOn)
    {
        RideId = rideId;
        DriverId = driverId;
        VehicleId = vehicleId;
        Duration = duration;
        Fare = fare;
        OccurredOn = occurredOn;
    }
}
