using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Scootly.Infrastructure.Persistence;

namespace Scootly.Infrastructure.Identity;

/// <summary>
/// JWT'ler kendi başına iptal edilemez. Her kullanıcı token'ı, verildiği andaki Identity güvenlik damgasını taşır;
/// rol değişikliği, parola değişikliği veya hesap silme damgayı değiştirir ve eski token'lar reddedilir.
/// Veritabanı sonucu kısa süre (<see cref="JwtOptions.SecurityStampCacheSeconds"/>) bellekte tutulur.
/// Hesap kilitlenmesi bilerek kontrol edilmez: başkasının yanlış parola denemeleri açık oturumları düşürmemeli.
/// </summary>
public sealed class UserSessionValidator
{
    private readonly ScootlyDbContext _dbContext;
    private readonly IMemoryCache _cache;
    private readonly TimeSpan _cacheDuration;

    public UserSessionValidator(ScootlyDbContext dbContext, IMemoryCache cache, IOptions<JwtOptions> options)
    {
        _dbContext = dbContext;
        _cache = cache;
        _cacheDuration = TimeSpan.FromSeconds(options.Value.SecurityStampCacheSeconds);
    }

    public async Task<bool> IsValidAsync(Guid userId, string? securityStamp, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(securityStamp))
            return false;

        var currentStamp = await GetCurrentStampAsync(userId, cancellationToken);

        return currentStamp is not null && string.Equals(currentStamp, securityStamp, StringComparison.Ordinal);
    }

    /// <summary>Bu süreçteki önbelleği hemen temizler (damga değiştikten sonra çağrılır).</summary>
    public void Invalidate(Guid userId) => _cache.Remove(CacheKey(userId));

    private async Task<string?> GetCurrentStampAsync(Guid userId, CancellationToken cancellationToken)
    {
        if (_cacheDuration > TimeSpan.Zero && _cache.TryGetValue(CacheKey(userId), out StampEntry? cached) && cached is not null)
            return cached.Stamp;

        // Silinmiş kullanıcının sonucu (null) da önbelleğe alınır.
        var stamp = await _dbContext.Users
            .AsNoTracking()
            .Where(u => u.Id == userId)
            .Select(u => u.SecurityStamp)
            .SingleOrDefaultAsync(cancellationToken);

        if (_cacheDuration > TimeSpan.Zero)
            _cache.Set(CacheKey(userId), new StampEntry(stamp), _cacheDuration);

        return stamp;
    }

    private static string CacheKey(Guid userId) => $"user-session-stamp:{userId:N}";

    private sealed record StampEntry(string? Stamp);
}
