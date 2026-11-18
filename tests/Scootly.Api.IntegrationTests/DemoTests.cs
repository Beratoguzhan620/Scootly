using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Scootly.Api.Demo;
using Scootly.Domain.Riding;
using Scootly.Infrastructure.Identity;
using Scootly.Infrastructure.Persistence;
using Scootly.Testing;
using Xunit;

namespace Scootly.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class DemoTests
{
    private readonly ScootlyApiFactory _factory;

    public DemoTests(ScootlyApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Demo_Bolgeyi_Araclari_Kurmali_Ve_Suruculeri_Surus_Yapip_Bitirmeli()
    {
        var bootstrap = Options.Create(new BootstrapOptions { FleetManagerEmail = "yonetici@demo.scootly.test", FleetManagerPassword = "Yonetici-Demo-2026" });
        await new IdentityBootstrapper(_factory.Services, bootstrap, NullLogger<IdentityBootstrapper>.Instance).StartAsync(CancellationToken.None);
        var worker = new DemoWorker(
            Options.Create(new DemoOptions { Enabled = true, RideLength = TimeSpan.Zero }), bootstrap, new TestClients(_factory),
            _factory.Services.GetRequiredService<IHostApplicationLifetime>(), TimeProvider.System, NullLogger<DemoWorker>.Instance);
        using var http = _factory.CreateClient();
        var completedBefore = await CompletedRidesAsync();

        await worker.SetUpAsync(http, CancellationToken.None);
        // Yeniden açılış aynı bölgeyi ve araçları bulur, çoğaltmaz.
        await worker.SetUpAsync(http, CancellationToken.None);
        for (var i = 0; i < 12; i++)
        {
            await worker.TickAsync(http, CancellationToken.None);
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<ScootlyDbContext>();
        Assert.Equal(1, await db.ServiceAreas.CountAsync(a => a.Name == "Samsun Merkez"));
        Assert.True(await db.Vehicles.CountAsync() >= 16);
        Assert.True(await CompletedRidesAsync() > completedBefore);
    }

    private async Task<int> CompletedRidesAsync()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<ScootlyDbContext>().Rides.CountAsync(r => r.Status == RideStatus.Completed);
    }

    private sealed class TestClients(ScootlyApiFactory factory) : IHttpClientFactory
    {
        public HttpClient CreateClient(string name) => factory.CreateClient();
    }
}
