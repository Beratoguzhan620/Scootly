using Microsoft.EntityFrameworkCore;
using Scootly.Application.Abstractions;
using Scootly.Domain.Riding;

namespace Scootly.Infrastructure.Persistence.Queries;

public sealed class RideReadService : IRideReadService
{
    private readonly ScootlyDbContext _dbContext;

    public RideReadService(ScootlyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<ActiveRideSummary>> GetActiveRidesAsync(int take, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Rides
            .AsNoTracking()
            .Where(r => r.Status == RideStatus.Active)
            .OrderByDescending(r => r.StartedAt)
            .Take(take)
            .Select(r => new ActiveRideSummary(r.Id, r.DriverId, r.VehicleId, r.StartedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ActiveRideSummary>> GetActiveRidesForDriverAsync(Guid driverId, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Rides
            .AsNoTracking()
            .Where(r => r.Status == RideStatus.Active && r.DriverId == driverId)
            .OrderByDescending(r => r.StartedAt)
            .Select(r => new ActiveRideSummary(r.Id, r.DriverId, r.VehicleId, r.StartedAt))
            .ToListAsync(cancellationToken);
    }
}
