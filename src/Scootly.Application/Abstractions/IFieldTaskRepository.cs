using Scootly.Domain.FieldOps;

namespace Scootly.Application.Abstractions;

public interface IFieldTaskRepository
{
    Task<bool> HasOpenTaskAsync(Guid vehicleId, FieldTaskType type, CancellationToken cancellationToken = default);

    Task AddAsync(FieldTask task, CancellationToken cancellationToken = default);
}
