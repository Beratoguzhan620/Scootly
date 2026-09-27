using Scootly.Domain.Common;

namespace Scootly.Domain.Riding.Events;

public sealed class RideCompletedEvent : IDomainEvent
{
    public RideId RideId { get; }
    public Guid DriverId { get; }
    public Guid VehicleId { get; }
    public TimeSpan Duration { get; }
    public double DistanceMeters { get; }
    public decimal Fare { get; }
    public DateTime OccurredOn { get; }

    public RideCompletedEvent(
        RideId rideId, Guid driverId, Guid vehicleId, TimeSpan duration, double distanceMeters, decimal fare, DateTime occurredOn)
    {
        RideId = rideId;
        DriverId = driverId;
        VehicleId = vehicleId;
        Duration = duration;
        DistanceMeters = distanceMeters;
        Fare = fare;
        OccurredOn = occurredOn;
    }
}
