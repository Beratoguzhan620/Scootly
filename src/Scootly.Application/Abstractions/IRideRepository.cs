using Scootly.Domain.Riding;

namespace Scootly.Application.Abstractions;

public interface IRideRepository
{
    Task<Ride?> GetByIdAsync(RideId id, CancellationToken cancellationToken = default);

    Task AddAsync(Ride ride, CancellationToken cancellationToken = default);
}