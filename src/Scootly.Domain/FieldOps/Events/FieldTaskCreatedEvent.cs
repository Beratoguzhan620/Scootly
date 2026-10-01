using Scootly.Domain.Common;

namespace Scootly.Domain.FieldOps.Events;

public sealed record FieldTaskCreatedEvent(Guid FieldTaskId, Guid VehicleId, FieldTaskType Type, DateTime OccurredOn) : IDomainEvent;