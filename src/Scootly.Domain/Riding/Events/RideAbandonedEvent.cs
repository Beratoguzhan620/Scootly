using Scootly.Domain.Common;

namespace Scootly.Domain.Riding.Events;

public sealed record RideAbandonedEvent(
    RideId RideId,
    Guid DriverId,
    Guid VehicleId,
    TimeSpan Duration,
    decimal Fare,
    DateTime OccurredOn) : IDomainEvent;
