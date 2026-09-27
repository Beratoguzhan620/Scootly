using Microsoft.EntityFrameworkCore;
using Scootly.Application.Abstractions;
using Scootly.Domain.Riding;

namespace Scootly.Infrastructure.Persistence.Repositories;

public sealed class RideRepository : IRideRepository
{
    private readonly ScootlyDbContext _dbContext;

    public RideRepository(ScootlyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<Ride?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
        => _dbContext.Rides.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public async Task AddAsync(Ride ride, CancellationToken cancellationToken = default)
        => await _dbContext.Rides.AddAsync(ride, cancellationToken);

    public Task<bool> HasActiveRideAsync(Guid driverId, CancellationToken cancellationToken = default)
        => _dbContext.Rides.AsNoTracking().AnyAsync(
            r => r.DriverId == driverId && r.Status == RideStatus.Active, cancellationToken);
}
