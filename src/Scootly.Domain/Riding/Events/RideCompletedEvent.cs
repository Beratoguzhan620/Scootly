using Scootly.Domain.Common;

namespace Scootly.Domain.Riding.Events;

public sealed record RideCompletedEvent(
    RideId RideId,
    Guid DriverId,
    Guid VehicleId,
    TimeSpan Duration,
    double DistanceMeters,
    decimal Fare,
    DateTime OccurredOn) : IDomainEvent;
