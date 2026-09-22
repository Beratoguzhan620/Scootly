using Microsoft.Extensions.Logging;
using StackExchange.Redis;

namespace Scootly.Infrastructure.Caching;

/// <summary>
/// Redis üzerinde süreli, tek sahipli kilit (50. gün).
/// </summary>
/// <remarks>
/// <para>
/// <b>Bu kilit bir DOĞRULUK GARANTİSİ DEĞİLDİR.</b> 50. günün "yaygın tuzaklar"
/// listesindeki ikinci madde tam olarak bu. Kilidin süresi dolduğunda sahibi
/// hâlâ çalışıyor olabilir: uzun bir çöp toplama duraklaması, ağ gecikmesi veya
/// sanal makinenin askıya alınması yeter. O anda ikinci bir süreç kilidi alır
/// ve <b>iki süreç aynı anda "kilit bende" sanır</b>. Redis tek düğümlü
/// olduğunda bu senaryo teorik değil, gözlenmiş bir durumdur.
/// </para>
/// <para>
/// Dolayısıyla bu kilit yalnızca bir <b>iyileştirmedir</b>: gereksiz işi
/// azaltır (aynı anda iki kopyanın aynı raporu üretmesi gibi). Doğruluğun
/// kendisi veritabanı seviyesinde durmalı — bu projede 38. günün sürüm damgası
/// ile. Sürüm damgası kaybolursa iki kişi aynı aracı kiralar; bu kilit
/// kaybolursa yalnızca biraz fazladan iş yapılır.
/// </para>
/// <para>
/// İki uygulama ayrıntısı önemli:
/// </para>
/// <list type="number">
///   <item>Kilit bir <b>jeton</b> (rastgele değer) ile alınıyor. Kilidi
///   bırakırken jeton karşılaştırılmasaydı, süresi dolmuş bir kilidin eski
///   sahibi, araya giren yeni sahibin kilidini silerdi.</item>
///   <item>Karşılaştırma ve silme tek bir <b>Lua betiğinde</b>, çünkü
///   "oku, eşitse sil" iki ayrı komut olsaydı arasında kilit el değiştirebilirdi
///   — 36. günde ölçtüğümüz oku-kontrol-yaz yarışının aynısı, bu sefer
///   Redis'te.</item>
/// </list>
/// </remarks>
public sealed class RedisDistributedLock
{
    /// <summary>Sahiplik doğrulayarak silen betik.</summary>
    private const string BirakmaBetigi = """
        if redis.call('get', KEYS[1]) == ARGV[1] then
            return redis.call('del', KEYS[1])
        else
            return 0
        end
        """;

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisDistributedLock> _logger;

    public RedisDistributedLock(IConnectionMultiplexer redis, ILogger<RedisDistributedLock> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    /// <summary>
    /// Kilidi almaya çalışır. Alınamazsa <c>null</c> döner — beklemez.
    /// </summary>
    /// <remarks>
    /// Beklememek bilinçli: bir HTTP isteğinin içinde kilit beklemek, kuyruğu
    /// istek iş parçacıklarında biriktirmek demek. Alınamayan kilitte doğru
    /// davranış çağırana "şu an olmaz" demek.
    /// </remarks>
    public async Task<Tutamak?> AcquireAsync(
        string key,
        TimeSpan timeToLive,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        if (timeToLive <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeToLive), "Kilit suresi sifirdan buyuk olmali.");
        }

        var tamAnahtar = RedisCacheService.Onek + key;
        var jeton = Guid.NewGuid().ToString("N");

        try
        {
            // When.NotExists = SET ... NX. Anahtar yoksa yazar ve true döner;
            // varsa hiçbir şey yapmaz ve false döner. Tek komutta atomik.
            //
            // Adlandırılmış argüman kullanılıyor: StackExchange.Redis'in bu
            // metodunun aşırı yüklemeleri sürümden sürüme değişti (araya
            // keepTtl parametresi girdi). Konumsal yazılsaydı, paket bir üst
            // sürüme çıktığında argümanlar sessizce kayabilirdi.
            var alindi = await _redis.GetDatabase()
                .StringSetAsync(tamAnahtar, jeton, expiry: timeToLive, when: When.NotExists);

            return alindi ? new Tutamak(_redis, tamAnahtar, jeton, _logger) : null;
        }
        catch (RedisConnectionException ex)
        {
            // Redis yoksa kilit de yok. Kilit bir iyileştirme olduğuna göre
            // doğru davranış, çağıranın işini kilitsiz yapmasına izin vermek
            // DEĞİL — çünkü çağıran kilidi aldığını sanırdı. null dönüyoruz:
            // "kilit alınamadı".
            _logger.LogWarning(ex, "Kilit alinamadi: Redis'e ulasilamiyor.");
            return null;
        }
    }

    /// <summary>Kilit tutamağı. <c>DisposeAsync</c> kilidi bırakır.</summary>
    public sealed class Tutamak : IAsyncDisposable
    {
        private readonly IConnectionMultiplexer _redis;
        private readonly string _anahtar;
        private readonly string _jeton;
        private readonly ILogger _logger;
        private bool _birakildi;

        internal Tutamak(IConnectionMultiplexer redis, string anahtar, string jeton, ILogger logger)
        {
            _redis = redis;
            _anahtar = anahtar;
            _jeton = jeton;
            _logger = logger;
        }

        public async ValueTask DisposeAsync()
        {
            if (_birakildi)
            {
                return;
            }

            _birakildi = true;

            try
            {
                await _redis.GetDatabase().ScriptEvaluateAsync(
                    BirakmaBetigi,
                    keys: [_anahtar],
                    values: [_jeton]);
            }
            catch (RedisConnectionException ex)
            {
                // Bırakılamayan kilit, süresi dolana kadar başkasını bekletir.
                // Zorunlu TTL'in gerekçesi bu: süresiz kilit, bir çökmede
                // sistemi kalıcı olarak durdururdu.
                _logger.LogWarning(ex, "Kilit birakilamadi; TTL dolana kadar tutulacak.");
            }
        }
    }
}
