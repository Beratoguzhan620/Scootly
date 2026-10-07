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

    public async Task<IReadOnlyList<FieldTaskSummary>> GetOpenTasksAsync(int take, CancellationToken cancellationToken = default)
    {
        return await _dbContext.FieldTasks
            .AsNoTracking()
            .Where(t => t.Status != FieldTaskStatus.Completed)
            .OrderBy(t => t.CreatedAt)
            .Take(take)
            .Select(t => new FieldTaskSummary(t.Id, t.VehicleId, t.Type.ToString(), t.Status.ToString(), t.AssignedTo, t.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<CompletedFieldTaskSummary>> GetRecentCompletedTasksAsync(int take, CancellationToken cancellationToken = default)
    {
        return await _dbContext.FieldTasks
            .AsNoTracking()
            .Where(t => t.Status == FieldTaskStatus.Completed)
            .OrderByDescending(t => t.CompletedAt)
            .Take(take)
            .Select(t => new CompletedFieldTaskSummary(t.Id, t.VehicleId, t.Type.ToString(), t.CompletedAt, t.PhotoObjectKey != null))
            .ToListAsync(cancellationToken);
    }

    public Task<string?> GetPhotoObjectKeyAsync(Guid fieldTaskId, CancellationToken cancellationToken = default)
    {
        return _dbContext.FieldTasks
            .AsNoTracking()
            .Where(t => t.Id == fieldTaskId)
            .Select(t => t.PhotoObjectKey)
            .SingleOrDefaultAsync(cancellationToken);
    }
}
