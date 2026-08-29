using Scootly.Domain.Fleet;
using Scootly.Domain.Riding;

namespace Scootly.Application.Abstractions;

public interface IApplicationDbContext
{
    IQueryable<Vehicle> Vehicles { get; }
    IQueryable<Ride> Rides { get; }

    void AddRide(Ride ride);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}