using System.Text.Json;
using Scootly.Application.Abstractions;

namespace Scootly.Infrastructure.Caching;

public sealed class NearbyVehicleCache
{
    private readonly ICacheService _cacheService;
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(5);

    public NearbyVehicleCache(ICacheService cacheService)
    {
        _cacheService = cacheService;
    }

    public async Task<T> GetOrSetAsync<T>(string key, Func<Task<T>> factory, CancellationToken cancellationToken = default)
    {
        var cached = await _cacheService.GetAsync(key, cancellationToken);

        if (cached is not null)
            return JsonSerializer.Deserialize<T>(cached)!;

        var value = await factory();
        var serialized = JsonSerializer.Serialize(value);
        await _cacheService.SetAsync(key, serialized, Ttl, cancellationToken);

        return value;
    }

    public Task InvalidateAsync(string key, CancellationToken cancellationToken = default)
        => _cacheService.RemoveAsync(key, cancellationToken);
}