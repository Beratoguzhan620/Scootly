using Scootly.Domain.Billing;

namespace Scootly.Application.Abstractions;

public interface IOutstandingDebtRepository
{
    Task AddAsync(OutstandingDebt debt, CancellationToken cancellationToken = default);
}
