using Scootly.Domain.Fleet;

namespace Scootly.Application.Abstractions;

public interface IVehicleRepository
{
    Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Vehicle>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
    Task<IReadOnlySet<Guid>> GetExistingIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default);
    Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken = default);

    /// <summary>Yazma tarafı değişmezi: bir sürücünün aynı anda yalnızca bir aktif rezervasyonu olabilir.</summary>
    Task<bool> HasActiveReservationAsync(Guid driverId, CancellationToken cancellationToken = default);
}
