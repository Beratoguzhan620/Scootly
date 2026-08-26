using Scootly.Domain.Common;
using Scootly.Domain.Geo;
using Scootly.Domain.Riding.Events;

namespace Scootly.Domain.Riding;

public sealed class Ride : AggregateRoot
{
    public Guid DriverId { get; }
    public Guid VehicleId { get; }
    public GeoPoint StartLocation { get; }
    public GeoPoint? EndLocation { get; private set; }
    public DateTime StartedAt { get; }
    public DateTime? EndedAt { get; private set; }
    public RideStatus Status { get; private set; }
    public decimal? Fare { get; private set; }

    public Ride(RideId id, Guid driverId, Guid vehicleId, GeoPoint startLocation, DateTime startedAt)
        : base(id.Value)
    {
        DriverId = driverId;
        VehicleId = vehicleId;
        StartLocation = startLocation;
        StartedAt = startedAt;
        Status = RideStatus.Active;

        AddDomainEvent(new RideStartedEvent(id, DateTime.UtcNow));
    }

    public void Complete(GeoPoint endLocation, DateTime endedAt)
    {
        if (Status != RideStatus.Active)
            throw new DomainException("Yalnızca aktif bir sürüş tamamlanabilir.");

        EndLocation = endLocation;
        EndedAt = endedAt;
        Status = RideStatus.Completed;

        var duration = endedAt - StartedAt;
        var distanceMeters = StartLocation.DistanceTo(endLocation);

        AddDomainEvent(new RideCompletedEvent(
            new RideId(Id), duration, distanceMeters, DateTime.UtcNow));
    }
}