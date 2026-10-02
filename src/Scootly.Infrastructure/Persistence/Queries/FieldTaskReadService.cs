using Microsoft.EntityFrameworkCore;
using Scootly.Application.Abstractions;
using Scootly.Domain.FieldOps;

namespace Scootly.Infrastructure.Persistence.Queries;

public sealed class FieldTaskReadService : IFieldTaskReadService
{
    private readonly ScootlyDbContext _dbContext;

    public FieldTaskReadService(ScootlyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<int> GetOpenTaskCountAsync(CancellationToken cancellationToken = default)
    {
        return _dbContext.FieldTasks
            .AsNoTracking()
            .CountAsync(t => t.Status != FieldTaskStatus.Completed, cancellationToken);
    }

    public async Task<IReadOnlyList<FieldTaskSummary>> GetOpenTasksAsync(CancellationToken cancellationToken = default)
    {
        return await _dbContext.FieldTasks
            .AsNoTracking()
            .Where(t => t.Status != FieldTaskStatus.Completed)
            .OrderBy(t => t.CreatedAt)
            .Select(t => new FieldTaskSummary(t.Id, t.VehicleId, t.Type.ToString(), t.Status.ToString(), t.AssignedTo, t.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}