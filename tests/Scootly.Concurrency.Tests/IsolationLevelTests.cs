using System.Data;
using Microsoft.Extensions.DependencyInjection;
using Scootly.Domain.Fleet;
using Scootly.Domain.Geo;
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
        var vehicleId = await SeedAvailableVehicleAsync();

        var tasks = Enumerable.Range(0, 20).Select(async _ =>
        {
            using var scope = _factory.Services.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<ScootlyDbContext>();

            return await IsolationLevelTestHelper.TryReserveWithIsolationLevel(
                dbContext, vehicleId, IsolationLevel.ReadCommitted);
        });

        var results = await Task.WhenAll(tasks);
        var successCount = results.Count(success => success);

        Assert.Equal(1, successCount);
    }

    private async Task<Guid> SeedAvailableVehicleAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScootlyDbContext>();

        var vehicle = new Vehicle(
            VehicleId.New(),
            new VehicleModel("Xiaomi", 25),
            new GeoPoint(41.0, 29.0),
            new BatteryLevel(80));

        dbContext.Vehicles.Add(vehicle);
        await dbContext.SaveChangesAsync();

        return vehicle.Id;
    }
}