using System;
using System.Collections.Generic;
using System.Text;

using Scootly.Domain.Common;
using Scootly.Domain.Fleet;
using Scootly.Domain.Geo;
using Scootly.Domain.Riding.Events;

namespace Scootly.Domain.Riding;

public sealed class Ride : AggregateRoot
{
    public Guid DriverId { get; }
    public VehicleId VehicleId { get; }

    public GeoPoint StartLocation { get; }
    public GeoPoint? EndLocation { get; private set; }

    public DateTime StartedAt { get; }
    public DateTime? EndedAt { get; private set; }

    public RideStatus Status { get; private set; }
    public decimal? Fare { get; private set; }

    // EF Core için — uygulama kodunda kullanılmaz
    private Ride() { }

    public Ride(RideId id, Guid driverId, VehicleId vehicleId, GeoPoint startLocation, DateTime startedAt)
        : base(id.Value)
    {
        if (driverId == Guid.Empty)
            throw new DomainException("Sürücü kimliği boş olamaz.");

        DriverId = driverId;
        VehicleId = vehicleId;
        StartLocation = startLocation;
        StartedAt = startedAt;
        Status = RideStatus.Active;

        AddDomainEvent(new RideStartedEvent(id, startedAt));
    }

    public void Complete(GeoPoint endLocation, DateTime endedAt)
    {
        if (Status != RideStatus.Active)
            throw new DomainException($"Sürüş {Status} durumundayken tamamlanamaz.");

        if (endedAt < StartedAt)
            throw new DomainException("Bitiş zamanı başlangıçtan önce olamaz.");

        EndLocation = endLocation;
        EndedAt = endedAt;

        var duration = endedAt - StartedAt;
        var distanceMeters = StartLocation.DistanceTo(endLocation);

        Status = RideStatus.Completed;

        AddDomainEvent(new RideCompletedEvent(new RideId(Id), duration, distanceMeters, endedAt));
    }
}
