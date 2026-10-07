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

        var query = ToSummaries(baseQuery.OrderBy(v => v.Id));

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedList<VehicleSummary>(items, filter.PageNumber, filter.PageSize, totalCount);
    }

    public async Task<VehicleSummary?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await ToSummaries(_dbContext.Vehicles.AsNoTracking().Where(v => v.Id == id))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<VehicleSummary>> GetLowBatteryVehiclesAsync(int take, CancellationToken cancellationToken = default)
    {
        return await ToSummaries(_dbContext.Vehicles
                .AsNoTracking()
                .Where(v => v.Battery.Percentage < BatteryLevel.LowThresholdPercentage)
                .OrderBy(v => v.Battery.Percentage))
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<VehicleSummary>> GetForMapAsync(bool onlyAvailable, int limit, CancellationToken cancellationToken = default)
    {
        var query = _dbContext.Vehicles.AsNoTracking();

        if (onlyAvailable)
            query = query.Where(v => v.Status == VehicleStatus.Available);

        return await ToSummaries(query.OrderBy(v => v.Id))
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    private static IQueryable<VehicleSummary> ToSummaries(IQueryable<Vehicle> query)
        => query.Select(v => new VehicleSummary(
            v.Id, v.Location.Latitude, v.Location.Longitude, v.Battery.Percentage,
            v.Status.ToString(), v.Model.Brand, v.Model.RangeKm));
}
