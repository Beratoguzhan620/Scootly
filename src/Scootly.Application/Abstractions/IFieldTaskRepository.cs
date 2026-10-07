using Scootly.Domain.FieldOps;

namespace Scootly.Application.Abstractions;

public interface IFieldTaskRepository
{
    Task<FieldTask?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    Task<bool> HasOpenTaskAsync(Guid vehicleId, FieldTaskType type, CancellationToken cancellationToken = default);

    Task AddAsync(FieldTask fieldTask, CancellationToken cancellationToken = default);
}
