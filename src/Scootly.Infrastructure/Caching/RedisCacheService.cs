using System.Text.Json;
using Microsoft.Extensions.Logging;
using Scootly.Application.Abstractions;
using StackExchange.Redis;

namespace Scootly.Infrastructure.Caching;

/// <summary>
/// <see cref="ICacheService"/>'in Redis uygulaması (47. gün).
/// </summary>
/// <remarks>
/// <para>
/// <b>Dağıtık önbelleğin çözdüğü problem.</b> 46. gündeki bellek içi önbellek
/// çalışıyordu — tek bir API kopyası olduğu sürece. İki kopya çalıştığında her
/// biri kendi belleğinde ayrı bir kopya tutar: birinde geçersiz kılınan girdi
/// diğerinde yaşamaya devam eder ve kullanıcı hangi kopyaya düştüğüne göre
/// farklı veri görür. Bu hata yük dengeleyici arkasında ortaya çıkar, yani
/// geliştirme makinesinde asla.
/// </para>
/// <para>
/// <b>Redis çökerse bu sınıf hata fırlatmaz.</b> Cache-aside deseninde önbellek
/// veritabanının önünde pasif bir katmandır; erişilemediğinde doğru davranış
/// "ıska" davranmaktır, isteği düşürmek değil. Aksi halde önbellek, sistemin
/// çalışması için zorunlu bir bileşene dönüşür — yani performans için eklenen
/// katman, yeni bir tek nokta arızası (single point of failure) olur.
/// </para>
/// <para>
/// <b>CancellationToken onurlandırılmıyor.</b> StackExchange.Redis'in asenkron
/// metotları iptal belirteci almıyor. Belirteci alıp yok saymak yerine bunu
/// yazıyoruz: sahte bir iptal desteği, iptalin çalıştığını sanan çağıranı
/// yanıltır.
/// </para>
/// </remarks>
public sealed class RedisCacheService : ICacheService
{
    /// <summary>
    /// Anahtar ad alanı. Redis tek bir anahtar uzayı: aynı sunucuyu başka bir
    /// uygulama kullanırsa çıplak anahtarlar çakışır.
    /// </summary>
    public const string Onek = "scootly:";

    private static readonly JsonSerializerOptions JsonAyarlari = new(JsonSerializerDefaults.Web);

    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<RedisCacheService> _logger;

    public RedisCacheService(IConnectionMultiplexer redis, ILogger<RedisCacheService> logger)
    {
        _redis = redis;
        _logger = logger;
    }

    public async Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        try
        {
            string? ham = await _redis.GetDatabase().StringGetAsync(Onek + key);

            if (string.IsNullOrEmpty(ham))
            {
                return null;
            }

            return JsonSerializer.Deserialize<T>(ham, JsonAyarlari);
        }
        catch (RedisConnectionException ex)
        {
            _logger.LogWarning(ex, "Redis'e ulasilamadi, iska olarak devam ediliyor.");
            return null;
        }
        catch (JsonException ex)
        {
            // Tipin biçimi değişmiş ve önbellekte eski biçimli JSON kalmış
            // olabilir. Bozuk girdiyi silip ıska davranmak, her istekte aynı
            // hatayı fırlatmaktan iyi.
            _logger.LogWarning(ex, "Onbellekteki deger cozulemedi, siliniyor.");
            await RemoveAsync(key, cancellationToken);
            return null;
        }
    }

    public async Task SetAsync<T>(
        string key,
        T value,
        TimeSpan timeToLive,
        CancellationToken cancellationToken = default)
        where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(value);

        if (timeToLive <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(timeToLive), "Yasam suresi sifirdan buyuk olmali.");
        }

        try
        {
            var json = JsonSerializer.Serialize(value, JsonAyarlari);

            // expiry adlandırılmış: bkz. RedisDistributedLock'taki aynı not.
            await _redis.GetDatabase().StringSetAsync(Onek + key, json, expiry: timeToLive);
        }
        catch (RedisConnectionException ex)
        {
            // Yazamamak bir hata değil, yalnızca bir sonraki okumanın ıska
            // olması demek.
            _logger.LogWarning(ex, "Onbellege yazilamadi.");
        }
    }

    public async Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        try
        {
            await _redis.GetDatabase().KeyDeleteAsync(Onek + key);
        }
        catch (RedisConnectionException ex)
        {
            // BURASI TEHLİKELİ VE BİLEREK LOGLANIYOR: silinemeyen bir girdi,
            // yaşam süresi dolana kadar bayat veri göstermeye devam eder.
            // Zorunlu TTL'in (bkz. ICacheService) asıl gerekçesi bu satır.
            _logger.LogError(ex, "Onbellek girdisi SILINEMEDI: {Anahtar}", key);
        }
    }
}
