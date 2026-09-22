using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scootly.Domain.Fleet;
using Scootly.Domain.Geo;
using Scootly.Infrastructure.Persistence;
using Xunit;

namespace Scootly.Concurrency.Tests;

public sealed class TrackingComparisonTests : IClassFixture<ConcurrencyTestFactory>
{
    private readonly ConcurrencyTestFactory _factory;

    public TrackingComparisonTests(ConcurrencyTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Tracking_Ile_NoTracking_Performansini_Karsilastir()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScootlyDbContext>();

        var random = new Random(42);

        for (var i = 0; i < 5_000; i++)
        {
            dbContext.Vehicles.Add(new Vehicle(
                VehicleId.New(),
                new VehicleModel("Xiaomi", 25),
                new GeoPoint(40.0 + random.NextDouble() * 2, 28.0 + random.NextDouble() * 2),
                new BatteryLevel(random.Next(1, 100))));
        }

        await dbContext.SaveChangesAsync();

        var trackingStopwatch = Stopwatch.StartNew();
        var trackedResults = await dbContext.Vehicles.ToListAsync();
        trackingStopwatch.Stop();

        var noTrackingStopwatch = Stopwatch.StartNew();
        var untrackedResults = await dbContext.Vehicles.AsNoTracking().ToListAsync();
        noTrackingStopwatch.Stop();

        throw new Xunit.Sdk.XunitException(
            $"Tracking ile: {trackingStopwatch.ElapsedMilliseconds} ms ({trackedResults.Count} kayıt) | " +
            $"AsNoTracking ile: {noTrackingStopwatch.ElapsedMilliseconds} ms ({untrackedResults.Count} kayıt)");
    }
}