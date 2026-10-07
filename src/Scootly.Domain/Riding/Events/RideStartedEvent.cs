using Scootly.Domain.Common;

namespace Scootly.Domain.Riding.Events;

public sealed record RideStartedEvent(RideId RideId, Guid DriverId, Guid VehicleId, DateTime OccurredOn) : IDomainEvent;
