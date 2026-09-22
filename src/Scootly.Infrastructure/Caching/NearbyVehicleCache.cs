using Microsoft.Extensions.Logging;
using Scootly.Application.Abstractions;
using Scootly.Application.Common;
using Scootly.Application.Fleet.Queries;
using StackExchange.Redis;

namespace Scootly.Infrastructure.Caching;

/// <summary>
/// Harita sorgusunun cache-aside önbelleği (48. gün).
/// </summary>
/// <remarks>
/// <para>
/// <b>Yaşam süresi 5 saniye.</b> Bu sayı bir denge kararı ve ADR 0017'de
/// gerekçelendirildi: uzun tutulursa kullanıcı kiralanmış bir aracı hâlâ
/// müsait sanar ve boşuna yürür; kısa tutulursa önbellek hiçbir işe yaramaz.
/// Beş saniye, harita ekranının yenilenme aralığından kısa — yani kullanıcı
/// aynı ekranda iki kez bakarsa ikincisi taze veri görüyor.
/// </para>
/// <para>
/// <b>Geçersizleştirme tek tek değil, önek silerek yapılıyor.</b> Bir aracın
/// durumu değiştiğinde onu hangi ızgara hücrelerinin kapsadığını hesaplamak
/// gerekirdi: araç birden fazla yarıçapın, birden fazla sayfanın içinde olabilir.
/// O hesabı yapmak yerine <c>yakin:*</c> önekinin tamamı siliniyor. Bedeli
/// dürüstçe: bir aracın değişmesi bütün harita önbelleğini düşürüyor. Beş
/// saniyelik TTL'de bunun maliyeti düşük — ama trafik arttığında bu karar
/// yeniden ölçülmeli (teknik borç).
/// </para>
/// </remarks>
public sealed class NearbyVehicleCache : INearbyVehicleCache
{
    public static readonly TimeSpan YasamSuresi = TimeSpan.FromSeconds(5);

    private readonly ICacheService _cache;
    private readonly IConnectionMultiplexer? _redis;
    private readonly ILogger<NearbyVehicleCache> _logger;

    public NearbyVehicleCache(
        ICacheService cache,
        ILogger<NearbyVehicleCache> logger,
        IConnectionMultiplexer? redis = null)
    {
        _cache = cache;
        _logger = logger;
        _redis = redis;
    }

    public async Task<PagedResult<NearbyVehicleDto>> GetOrSetAsync(
        FindNearbyVehiclesQuery query,
        Func<CancellationToken, Task<PagedResult<NearbyVehicleDto>>> fromDatabase,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(fromDatabase);

        var anahtar = CacheKeys.YakinAraclar(query);

        // 1. İSABET yolu.
        var onbellekten = await _cache.GetAsync<PagedResult<NearbyVehicleDto>>(anahtar, cancellationToken);

        if (onbellekten is not null)
        {
            return onbellekten;
        }

        // 2. ISKA yolu: veritabanına git.
        //
        // Burada kilit YOK. Aynı anahtar için aynı anda gelen N istek, N kez
        // veritabanına gider ("cache stampede"). Bunu kilitle çözmek, her
        // ıskada bir dağıtık kilit almak demek — ve o kilidin maliyeti,
        // önlediği fazladan sorgudan yüksek. Beş saniyelik TTL'de aynı
        // anahtara aynı anda gelen istek sayısı zaten sınırlı. Trafik
        // arttığında yeniden ölçülecek (teknik borç).
        var sonuc = await fromDatabase(cancellationToken);

        await _cache.SetAsync(anahtar, sonuc, YasamSuresi, cancellationToken);

        return sonuc;
    }

    public async Task InvalidateAsync(CancellationToken cancellationToken = default)
    {
        if (_redis is null)
        {
            // Bellek içi önbellekte önek silme yok. Bu durumda tek güvence
            // yaşam süresi — beş saniye. Tek kopya çalışan geliştirme
            // ortamında kabul edilebilir, üretimde değil.
            _logger.LogDebug("Onek silme yalnizca Redis ile; TTL'e birakiliyor.");
            return;
        }

        try
        {
            var desen = RedisCacheService.Onek + CacheKeys.YakinAraclarOneki + "*";

            foreach (var uçNokta in _redis.GetEndPoints())
            {
                var sunucu = _redis.GetServer(uçNokta);

                // Replika üzerinde SCAN çalıştırmak gereksiz iş; yazma zaten
                // primary'de.
                if (sunucu.IsReplica)
                {
                    continue;
                }

                // KEYS değil SCAN. KEYS tüm anahtar uzayını tek seferde tarar
                // ve Redis tek iş parçacıklı olduğu için o süre boyunca BÜTÜN
                // istekleri bekletir. SCAN parça parça ilerler.
                await foreach (var anahtar in sunucu.KeysAsync(pattern: desen, pageSize: 250)
                                   .WithCancellation(cancellationToken))
                {
                    await _redis.GetDatabase().KeyDeleteAsync(anahtar);
                }
            }
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogError(ex, "Harita onbellegi TEMIZLENEMEDI; TTL dolana kadar bayat veri gorunebilir.");
        }
    }
}
