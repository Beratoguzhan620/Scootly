using Microsoft.Extensions.Caching.Memory;
using Scootly.Application.Abstractions;

namespace Scootly.Infrastructure.Caching;

/// <summary>
/// <see cref="ICacheService"/>'in bellek içi uygulaması (46. gün).
/// </summary>
/// <remarks>
/// <para>
/// 46. günün hali. 47. günde <see cref="RedisCacheService"/> ile değiştirildi
/// ama SİLİNMEDİ: Redis'in çalışmadığı ortamlarda (birim testler, tek kopya
/// çalışan geliştirme makinesi) hâlâ geçerli bir seçenek.
/// </para>
/// <para>
/// <b>Neden tek başına yetmiyor.</b> Bu önbellek uygulamanın kendi belleğinde.
/// İki API kopyası çalıştığında iki ayrı önbellek olur: birinde silinen girdi
/// diğerinde yaşar, kullanıcı hangi kopyaya düştüğüne göre farklı veri görür.
/// Tek makinede test edildiği sürece kusursuz görünür — bu yüzden 47. günde
/// iki kopyayı aynı anda çalıştırıp doğrulamak istenmişti.
/// </para>
/// <para>
/// <c>SizeLimit</c> ayarlanmış bir <see cref="IMemoryCache"/> bekliyor. Sınırsız
/// bir bellek içi önbellek, girdi sayısı girdi çeşidiyle birlikte büyüdüğünde
/// (harita sorgusunda her koordinat bir anahtar) uygulamayı bellek yetersizliğine
/// kadar götürür.
/// </para>
/// </remarks>
public sealed class MemoryCacheService : ICacheService
{
    private readonly IMemoryCache _cache;

    public MemoryCacheService(IMemoryCache cache)
    {
        _cache = cache;
    }

    public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        where T : class
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        return Task.FromResult(_cache.Get<T>(key));
    }

    public Task SetAsync<T>(
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

        _cache.Set(key, value, new MemoryCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = timeToLive,

            // Her girdi 1 birim. SizeLimit ayarlıysa bu zorunlu — atlanırsa
            // Set çağrısı çalışma zamanında InvalidOperationException fırlatır.
            Size = 1
        });

        return Task.CompletedTask;
    }

    public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        _cache.Remove(key);

        return Task.CompletedTask;
    }
}
