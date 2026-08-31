using Scootly.Domain.Riding;

namespace Scootly.Application.Abstractions;

public interface IRideRepository
{
    Task<Ride?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Ride ride, CancellationToken cancellationToken = default);
}