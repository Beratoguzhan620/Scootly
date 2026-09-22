using Scootly.Application.Abstractions;
using StackExchange.Redis;

namespace Scootly.Infrastructure.Caching;

public sealed class RedisCacheService : ICacheService
{
    private readonly IConnectionMultiplexer _connectionMultiplexer;

    public RedisCacheService(IConnectionMultiplexer connectionMultiplexer)
    {
        _connectionMultiplexer = connectionMultiplexer;
    }

    private IDatabase Database => _connectionMultiplexer.GetDatabase();

    public async Task<string?> GetAsync(string key, CancellationToken cancellationToken = default)
    {
        var value = await Database.StringGetAsync(key);
        return value.HasValue ? value.ToString() : null;
    }

    public async Task SetAsync(string key, string value, TimeSpan expiry, CancellationToken cancellationToken = default)
    {
        await Database.StringSetAsync(key, value, expiry);
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        await Database.KeyDeleteAsync(key);
    }
}