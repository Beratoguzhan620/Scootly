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

    public async Task<Ride?> GetByIdAsync(RideId id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Rides
            .FirstOrDefaultAsync(ride => ride.Id == id.Value, cancellationToken);
    }

    public async Task AddAsync(Ride ride, CancellationToken cancellationToken = default)
    {
        await _dbContext.Rides.AddAsync(ride, cancellationToken);
    }
}