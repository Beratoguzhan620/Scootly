using Scootly.Application.Abstractions;

namespace Scootly.Infrastructure.Persistence;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly ScootlyDbContext _dbContext;

    public UnitOfWork(ScootlyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
        => _dbContext.SaveChangesAsync(cancellationToken);
}