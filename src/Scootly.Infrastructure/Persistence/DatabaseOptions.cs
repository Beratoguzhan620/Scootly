namespace Scootly.Infrastructure.Persistence;

/// <summary>Veritabanı bağlantı dizesinin uygulama açılışında doğrulanması için.</summary>
public sealed class DatabaseOptions
{
    public string? ConnectionString { get; set; }
}
