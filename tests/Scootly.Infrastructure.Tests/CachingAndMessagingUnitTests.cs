using System.Text;
using Microsoft.Extensions.Logging.Abstractions;
using Scootly.Application.Abstractions;
using Scootly.Infrastructure.Caching;
using Scootly.Infrastructure.Messaging.Consumers;
using Xunit;

namespace Scootly.Infrastructure.Tests;

public sealed class NearbyVehicleCacheTests
{
    private sealed class BrokenCache : ICacheService
    {
        public Task<string?> GetAsync(string key, CancellationToken cancellationToken = default) => throw new InvalidOperationException("redis yok");
        public Task SetAsync(string key, string value, TimeSpan expiry, CancellationToken cancellationToken = default) => throw new InvalidOperationException("redis yok");
        public Task RemoveAsync(string key, CancellationToken cancellationToken = default) => throw new InvalidOperationException("redis yok");
    }

    [Fact]
    public async Task Onbellek_Erisilemezse_Kaynaktan_Cevaplanmali()
    {
        var cache = new NearbyVehicleCache(new BrokenCache(), NullLogger<NearbyVehicleCache>.Instance);

        var value = await cache.GetOrSetAsync("key", _ => Task.FromResult(42));
        await cache.InvalidateAsync("key");

        Assert.Equal(42, value);
    }
}

public sealed class DeathHeaderTests
{
    private static Dictionary<string, object?> Entry(string queue, string reason, long count) => new()
    {
        ["queue"] = Encoding.UTF8.GetBytes(queue),
        ["reason"] = Encoding.UTF8.GetBytes(reason),
        ["count"] = count
    };

    [Fact]
    public void Baslik_Yoksa_Sifir_Donmeli()
    {
        Assert.Equal(0, DeathHeader.GetRejectionCount(null, "q"));
        Assert.Equal(0, DeathHeader.GetRejectionCount(new Dictionary<string, object?>(), "q"));
    }

    [Fact]
    public void Yalnizca_Ilgili_Kuyrugun_Reddedilme_Sayisi_Okunmali()
    {
        var headers = new Dictionary<string, object?>
        {
            [DeathHeader.HeaderName] = new List<object>
            {
                Entry("q", "rejected", 2),
                Entry("q.retry", "expired", 2),
                Entry("baska", "rejected", 7)
            }
        };

        Assert.Equal(2, DeathHeader.GetRejectionCount(headers, "q"));
    }
}
