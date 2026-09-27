using Microsoft.Extensions.Caching.Memory;
using Scootly.Application.Abstractions;

namespace Scootly.Infrastructure.Caching;

/// <summary>Redis yapılandırılmadığında (tek instance, test) kullanılan süreç içi önbellek.</summary>
public sealed class InMemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;

    public InMemoryCacheService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
        => Task.FromResult(_cache.TryGetValue(key, out string? value) ? value : null);

    public Task SetAsync(string key, string value, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        _cache.Set(key, value, expiry);
        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        _cache.Remove(key);
        return Task.CompletedTask;
    }
}
