using Microsoft.EntityFrameworkCore;
using Scootly.Application.Abstractions;
using Scootly.Domain.FieldOps;

namespace Scootly.Infrastructure.Persistence.Repositories;

public sealed class FieldTaskRepository : IFieldTaskRepository
{
    private readonly ScootlyDbContext _dbContext;

    public FieldTaskRepository(ScootlyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<FieldTask?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.FieldTasks.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    public Task<bool> HasOpenTaskAsync(Guid vehicleId, FieldTaskType type, CancellationToken cancellationToken = default)
        => _dbContext.FieldTasks.AsNoTracking().AnyAsync(
            t => t.VehicleId == vehicleId && t.Type == type && t.Status != FieldTaskStatus.Completed,
            cancellationToken);

    public async Task AddAsync(FieldTask fieldTask, CancellationToken cancellationToken = default)
        => await _dbContext.FieldTasks.AddAsync(fieldTask, cancellationToken);
}
