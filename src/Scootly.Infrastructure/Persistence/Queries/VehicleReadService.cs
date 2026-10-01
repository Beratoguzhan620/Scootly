using Microsoft.EntityFrameworkCore;
using Scootly.Application.Abstractions;
using Scootly.Domain.Fleet;

namespace Scootly.Infrastructure.Persistence.Queries;

public sealed class VehicleReadService : IVehicleReadService
{
    private readonly ScootlyDbContext _dbContext;

    public VehicleReadService(ScootlyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PagedList<VehicleSummary>> GetVehiclesAsync(VehicleFilter filter, CancellationToken cancellationToken = default)
    {
        var baseQuery = _dbContext.Vehicles.AsNoTracking();

        if (filter.MinLatitude is { } minLatitude) baseQuery = baseQuery.Where(v => v.Location.Latitude >= minLatitude);
        if (filter.MaxLatitude is { } maxLatitude) baseQuery = baseQuery.Where(v => v.Location.Latitude <= maxLatitude);
        if (filter.MinLongitude is { } minLongitude) baseQuery = baseQuery.Where(v => v.Location.Longitude >= minLongitude);
        if (filter.MaxLongitude is { } maxLongitude) baseQuery = baseQuery.Where(v => v.Location.Longitude <= maxLongitude);
        if (filter.OnlyAvailable) baseQuery = baseQuery.Where(v => v.Status == VehicleStatus.Available);

        var query = baseQuery
            .OrderBy(v => v.Id)
            .Select(v => new VehicleSummary(
                v.Id, v.Location.Latitude, v.Location.Longitude, v.Battery.Percentage,
                v.Status.ToString(), v.Model.Brand, v.Model.RangeKm));

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedList<VehicleSummary>(items, filter.PageNumber, filter.PageSize, totalCount);
    }

    public async Task<VehicleSummary?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Vehicles
            .AsNoTracking()
            .Where(v => v.Id == id)
            .Select(v => new VehicleSummary(
                v.Id, v.Location.Latitude, v.Location.Longitude, v.Battery.Percentage,
                v.Status.ToString(), v.Model.Brand, v.Model.RangeKm))
            .FirstOrDefaultAsync(cancellationToken);
    }
}