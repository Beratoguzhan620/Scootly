using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scootly.Infrastructure.Persistence;
using Scootly.Testing;

namespace Scootly.Concurrency.Tests;

/// <summary>Ortak test sunucusu + her sorguyu sayan EF interceptor'ı (N+1 ölçümleri için).</summary>
public sealed class ConcurrencyTestFactory : ScootlyApiFactory
{
    protected override void ConfigureTestServices(IServiceCollection services)
    {
        services.ConfigureDbContext<ScootlyDbContext>(options => options.AddInterceptors(new QueryCounter.CountingInterceptor()));
    }
}

/// <summary>
/// Ölçüm testleri regresyon testi değildir: süreleri/sayıları çıktıya yazar ve başarısız olmaz.
/// Hızlı geri bildirim için hariç tutulabilir: <c>dotnet test --filter "Category!=Measurement"</c>.
/// </summary>
public static class TestCategories
{
    public const string Key = "Category";
    public const string Measurement = "Measurement";
    public const string Experiment = "Experiment";
}
