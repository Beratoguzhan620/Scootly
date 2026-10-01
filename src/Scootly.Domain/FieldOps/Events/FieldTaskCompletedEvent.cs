using Scootly.Domain.Common;

namespace Scootly.Domain.FieldOps.Events;

public sealed record FieldTaskCompletedEvent(Guid FieldTaskId, Guid OperatorId, DateTime OccurredOn) : IDomainEvent;