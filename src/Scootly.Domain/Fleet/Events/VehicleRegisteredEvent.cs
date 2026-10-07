using Scootly.Domain.Common;

namespace Scootly.Domain.Fleet.Events;

public sealed record VehicleRegisteredEvent(VehicleId VehicleId, DateTime OccurredOn) : IDomainEvent;
