using Scootly.Application.Abstractions;
using Scootly.Domain.Billing;

namespace Scootly.Infrastructure.Persistence.Repositories;

public sealed class OutstandingDebtRepository : IOutstandingDebtRepository
{
    private readonly ScootlyDbContext _dbContext;

    public OutstandingDebtRepository(ScootlyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task AddAsync(OutstandingDebt debt, CancellationToken cancellationToken = default)
    {
        await _dbContext.OutstandingDebts.AddAsync(debt, cancellationToken);
    }
}
