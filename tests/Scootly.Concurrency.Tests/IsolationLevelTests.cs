using System.Data;
using Microsoft.Extensions.DependencyInjection;
using Scootly.Infrastructure.Persistence;
using Xunit;

namespace Scootly.Concurrency.Tests;

public sealed class IsolationLevelTests : IClassFixture<ConcurrencyTestFactory>
{
    private readonly ConcurrencyTestFactory _factory;

    public IsolationLevelTests(ConcurrencyTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Xmin_Korumasi_ReadCommitted_Seviyesinde_Bile_Calismali()
    {
        var vehicle = await _factory.SeedVehicleAsync();

        var tasks = Enumerable.Range(0, 20).Select(async _ =>
        {
            using var scope = _factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ScootlyDbContext>();

            return await IsolationLevelTestHelper.TryReserveWithIsolationLevel(dbContext, vehicle.Id, IsolationLevel.ReadCommitted);
        });

        var results = await Task.WhenAll(tasks);

        Assert.Equal(1, results.Count(success => success));
    }
}
