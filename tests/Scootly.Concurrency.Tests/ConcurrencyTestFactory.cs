using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Scootly.Application.Abstractions;
using Scootly.Application.IntegrationEvents;
using Scootly.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Scootly.Concurrency.Tests;

public sealed class ConcurrencyTestFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder("postgres:16")
        .WithDatabase("scootly_concurrency_test")
        .WithUsername("postgres")
        .WithPassword("test_sifre")
        .Build();

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        // 62. gun: API acilista RabbitMq ayarlarini dogruluyor (parola yoksa durur).
        // Testlerde kuyruk yok; parola yer tutucu, yayinlayici ise asagida
        // bellekte kaydeden bir sahteyle degistiriliyor. Aksi halde her surus
        // bitirme istegi RabbitMQ'ya baglanmaya calisip zaman asimini bekler.
        builder.UseSetting("RabbitMq:Password", "test-kuyruk-yok");

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEventPublisher>();
            services.AddSingleton<IEventPublisher, BellekYayinlayici>();

            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ScootlyDbContext>));

            if (descriptor is not null)
                services.Remove(descriptor);

            services.AddDbContext<ScootlyDbContext>(options =>
            {
                options.UseNpgsql(_postgresContainer.GetConnectionString());
                options.AddInterceptors(new QueryCounter.CountingInterceptor());
            });
        });
    }

    public async Task InitializeAsync()
    {
        await _postgresContainer.StartAsync();

        using var scope = Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScootlyDbContext>();
        await dbContext.Database.MigrateAsync();
    }

    public new async Task DisposeAsync()
    {
        await _postgresContainer.StopAsync();
    }
}

/// <summary>Olaylari kuyruga degil bellege yazan yayinlayici (yalnizca testler).</summary>
public sealed class BellekYayinlayici : IEventPublisher
{
    private readonly System.Collections.Concurrent.ConcurrentQueue<IIntegrationEvent> _olaylar = new();

    public IReadOnlyCollection<IIntegrationEvent> Olaylar => _olaylar;

    public Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent, IHasEventName
    {
        _olaylar.Enqueue(integrationEvent);
        return Task.CompletedTask;
    }
}
