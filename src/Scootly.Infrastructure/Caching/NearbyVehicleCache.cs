using System.Text.Json;
using Microsoft.Extensions.Logging;
using Scootly.Application.Abstractions;

namespace Scootly.Infrastructure.Caching;

/// <summary>
/// Cache-aside, "en iyi çaba" (best-effort) ilkesiyle: önbellek erişilemezse istek hata vermez,
/// doğrudan kaynaktan (veritabanı) cevaplanır ve durum yalnızca loglanır.
/// </summary>
public sealed class NearbyVehicleCache
{
    private static readonly TimeSpan Ttl = TimeSpan.FromSeconds(5);

    private readonly ICacheService _cacheService;
    private readonly ILogger<NearbyVehicleCache> _logger;

    public NearbyVehicleCache(ICacheService cacheService, ILogger<NearbyVehicleCache> logger)
    {
        _cacheService = cacheService;
        _logger = logger;
    }

    public async Task<T> GetOrSetAsync<T>(string key, Func<CancellationToken, Task<T>> factory, CancellationToken cancellationToken = default)
    {
        try
        {
            var cached = await _cacheService.GetAsync(key, cancellationToken);

            if (cached is not null)
                return JsonSerializer.Deserialize<T>(cached)!;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Önbellek okunamadı ({Key}); veritabanından cevaplanıyor.", key);
        }

        var value = await factory(cancellationToken);

        try
        {
            await _cacheService.SetAsync(key, JsonSerializer.Serialize(value), Ttl, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Önbelleğe yazılamadı ({Key}).", key);
        }

        return value;
    }

    public async Task InvalidateAsync(string key, CancellationToken cancellationToken = default)
    {
        try
        {
            await _cacheService.RemoveAsync(key, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // En kötü durumda eski veri TTL (5 sn) boyunca görünür.
            _logger.LogWarning(ex, "Önbellek anahtarı silinemedi ({Key}).", key);
        }
    }
}
