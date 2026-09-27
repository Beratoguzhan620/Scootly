using Microsoft.EntityFrameworkCore;
using Scootly.Application.Abstractions;
using Scootly.Domain.Fleet;

namespace Scootly.Infrastructure.Persistence.Repositories;

public sealed class VehicleRepository : IVehicleRepository
{
    private readonly ScootlyDbContext _dbContext;

    public VehicleRepository(ScootlyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.Vehicles.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Vehicle>> GetByIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
        => await _dbContext.Vehicles.Where(v => ids.Contains(v.Id)).ToListAsync(cancellationToken);

    public async Task<IReadOnlySet<Guid>> GetExistingIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken = default)
    {
        var existing = await _dbContext.Vehicles
            .AsNoTracking()
            .Where(v => ids.Contains(v.Id))
            .Select(v => v.Id)
            .ToListAsync(cancellationToken);

        return existing.ToHashSet();
    }

    public async Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken = default)
        => await _dbContext.Vehicles.AddAsync(vehicle, cancellationToken);

    public Task<bool> HasActiveReservationAsync(Guid driverId, CancellationToken cancellationToken = default)
        => _dbContext.Vehicles.AsNoTracking().AnyAsync(
            v => v.ReservedBy == driverId && v.Status == VehicleStatus.Reserved, cancellationToken);
}
