using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scootly.Application;
using Scootly.Application.Abstractions;
using Scootly.Infrastructure;
using Scootly.Infrastructure.Persistence;
using Scootly.Testing;
using Testcontainers.PostgreSql;
using Xunit;

namespace Scootly.Worker.Tests;

public sealed class MutableClock : IClock
{
    public MutableClock(DateTime utcNow)
    {
        UtcNow = utcNow;
    }

    public DateTime UtcNow { get; set; }
}

/// <summary>
/// Worker'ın kendi kayıtlarıyla (Application + Infrastructure) kurulmuş servisler; gerçek PostgreSQL,
/// sabitlenebilir saat ve sahte ödeme sağlayıcısı. Mesajlaşma kapalıdır (işler doğrudan çağrılır).
/// </summary>
public sealed class WorkerFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16")
        .WithDatabase("scootly_worker_test")
        .WithUsername("postgres")
        .WithPassword("test_sifre")
        .Build();

    public ServiceProvider Services { get; private set; } = null!;

    public MutableClock Clock { get; } = new(DateTime.SpecifyKind(DateTime.UtcNow.Date.AddHours(12), DateTimeKind.Utc));

    public FakePaymentGateway PaymentGateway { get; } = new();

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString(),
                ["Messaging:Enabled"] = "false",
                ["Redis:ConnectionString"] = string.Empty
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddScootlyApplication();
        services.AddScootlyPayments();
        services.AddScootlyInfrastructure(configuration);

        // Son kayıt kazanır: işler ve handler'lar test saatini ve sahte sağlayıcıyı kullanır.
        services.AddSingleton<IClock>(Clock);
        services.AddSingleton<IPaymentGateway>(PaymentGateway);

        Services = services.BuildServiceProvider();

        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ScootlyDbContext>().Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    public async Task SeedAsync(params object[] entities)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ScootlyDbContext>();

        db.AddRange(entities);
        await db.SaveChangesAsync();
    }

    public async Task<T> QueryAsync<T>(Func<ScootlyDbContext, Task<T>> query)
    {
        using var scope = Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<ScootlyDbContext>());
    }

    public async Task RunAsync(Jobs.PeriodicJob job)
    {
        using var scope = Services.CreateScope();
        await job.RunOnceAsync(scope.ServiceProvider, CancellationToken.None);
    }
}

[CollectionDefinition(Name)]
public sealed class WorkerCollection : ICollectionFixture<WorkerFixture>
{
    public const string Name = "worker";
}
