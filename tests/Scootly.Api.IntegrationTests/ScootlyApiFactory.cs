using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scootly.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Scootly.Api.IntegrationTests;

public sealed class ScootlyApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder("postgres:16")
        .WithDatabase("scootly_test")
        .WithUsername("postgres")
        .WithPassword("test_sifre")
        .Build();

    protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ScootlyDbContext>));

            if (descriptor is not null)
                services.Remove(descriptor);

            services.AddDbContext<ScootlyDbContext>(options =>
                options.UseNpgsql(_postgresContainer.GetConnectionString()));
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