using Scootly.Domain.Riding;

namespace Scootly.Application.Abstractions;

public interface IRideRepository
{
    Task<Ride?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task AddAsync(Ride ride, CancellationToken cancellationToken = default);

    /// <summary>Yazma tarafı değişmezi: bir sürücünün aynı anda yalnızca bir aktif sürüşü olabilir.</summary>
    Task<bool> HasActiveRideAsync(Guid driverId, CancellationToken cancellationToken = default);
}
