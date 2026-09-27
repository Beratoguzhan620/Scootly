namespace Scootly.Infrastructure.Caching;

public sealed class RedisOptions
{
    public const string SectionName = "Redis";

    /// <summary>
    /// StackExchange.Redis bağlantı dizesi (parola içerebilir; user-secrets / ortam değişkeninde tutulur).
    /// Boş bırakılırsa süreç içi önbellek kullanılır (yalnızca tek instance ve test için uygundur).
    /// </summary>
    public string? ConnectionString { get; init; }
}
