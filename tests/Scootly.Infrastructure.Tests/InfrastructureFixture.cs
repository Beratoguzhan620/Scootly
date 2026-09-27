using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scootly.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Testcontainers.RabbitMq;
using Xunit;

namespace Scootly.Infrastructure.Tests;

/// <summary>
/// Gerçek PostgreSQL ve RabbitMQ container'ları üzerinde, uygulamanın kendi altyapı kaydıyla
/// (AddScootlyInfrastructure) kurulmuş bir servis sağlayıcı.
/// </summary>
public sealed class InfrastructureFixture : IAsyncLifetime
{
    public const int RetryDelayMilliseconds = 200;
    public const int MaxRetryAttempts = 2;

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16")
        .WithDatabase("scootly_infra_test")
        .WithUsername("postgres")
        .WithPassword("test_sifre")
        .Build();

    private readonly RabbitMqContainer _rabbitMq = new RabbitMqBuilder("rabbitmq:3.13-management")
        .WithUsername("scootly")
        .WithPassword("test_sifre")
        .Build();

    public ServiceProvider Services { get; private set; } = null!;

    public async ValueTask InitializeAsync()
    {
        await Task.WhenAll(_postgres.StartAsync(), _rabbitMq.StartAsync());

        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _postgres.GetConnectionString(),
                ["RabbitMq:HostName"] = _rabbitMq.Hostname,
                ["RabbitMq:Port"] = _rabbitMq.GetMappedPublicPort(5672).ToString(),
                ["RabbitMq:UserName"] = "scootly",
                ["RabbitMq:Password"] = "test_sifre",
                // Barındırılan outbox servisi başlatılmaz; testler işlemciyi doğrudan çağırır.
                ["Messaging:Enabled"] = "false",
                ["Messaging:RetryDelayMilliseconds"] = RetryDelayMilliseconds.ToString(),
                ["Messaging:MaxRetryAttempts"] = MaxRetryAttempts.ToString()
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddScootlyInfrastructure(configuration);

        Services = services.BuildServiceProvider();

        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ScootlyDbContext>().Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await Services.DisposeAsync();
        await Task.WhenAll(_postgres.DisposeAsync().AsTask(), _rabbitMq.DisposeAsync().AsTask());
    }
}

[CollectionDefinition(Name)]
public sealed class InfrastructureCollection : ICollectionFixture<InfrastructureFixture>
{
    public const string Name = "infrastructure";
}
