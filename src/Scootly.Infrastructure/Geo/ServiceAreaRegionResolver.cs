using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Scootly.Application.Abstractions;
using Scootly.Domain.Geo;
using Scootly.Infrastructure.Persistence;

namespace Scootly.Infrastructure.Geo;

/// <summary>
/// Bir konumun hangi hizmet bölgesine düştüğünü bulur (canlı bildirim grupları bölge adına göre kurulur).
/// Bölgeler nadiren değiştiği için kısa süre bellekte tutulur. Önbellek süreç başınadır: yeni bölgeyi ekleyen
/// kopya hemen, diğer kopyalar en geç <see cref="CacheDuration"/> sonra görür.
/// </summary>
public sealed class ServiceAreaRegionResolver : IRegionResolver
{
    private const string CacheKey = "service-areas";
    public static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(1);

    private readonly ScootlyDbContext _dbContext;
    private readonly IMemoryCache _cache;
    private readonly GeofenceEvaluator _geofenceEvaluator = new();

    public ServiceAreaRegionResolver(ScootlyDbContext dbContext, IMemoryCache cache)
    {
        _dbContext = dbContext;
        _cache = cache;
    }

    public async Task<string> ResolveRegionAsync(double latitude, double longitude, CancellationToken cancellationToken = default)
    {
        var areas = await GetAreasAsync(cancellationToken);
        var point = new GeoPoint(latitude, longitude);

        return areas.FirstOrDefault(area => _geofenceEvaluator.IsInsideArea(point, area))?.Name
               ?? IRegionResolver.DefaultRegion;
    }

    public async Task<bool> IsKnownRegionAsync(string regionName, CancellationToken cancellationToken = default)
    {
        if (string.Equals(regionName, IRegionResolver.DefaultRegion, StringComparison.Ordinal))
            return true;

        var areas = await GetAreasAsync(cancellationToken);
        return areas.Any(area => string.Equals(area.Name, regionName, StringComparison.Ordinal));
    }

    private async Task<IReadOnlyList<ServiceArea>> GetAreasAsync(CancellationToken cancellationToken)
    {
        if (_cache.TryGetValue(CacheKey, out IReadOnlyList<ServiceArea>? cached) && cached is not null)
            return cached;

        var areas = await _dbContext.ServiceAreas.AsNoTracking().ToListAsync(cancellationToken);
        _cache.Set(CacheKey, (IReadOnlyList<ServiceArea>)areas, CacheDuration);

        return areas;
    }

    /// <summary>Yeni bölge eklendiğinde önbellek hemen tazelensin.</summary>
    public static void Invalidate(IMemoryCache cache) => cache.Remove(CacheKey);
}
