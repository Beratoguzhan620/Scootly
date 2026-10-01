using Scootly.Domain.Common;

namespace Scootly.Domain.FieldOps.Events;

public sealed record FieldTaskAssignedEvent(Guid FieldTaskId, Guid OperatorId, DateTime OccurredOn) : IDomainEvent;