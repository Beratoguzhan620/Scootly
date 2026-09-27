using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scootly.Infrastructure.Persistence;
using Scootly.Testing;
using Xunit;

namespace Scootly.Concurrency.Tests;

/// <summary>Değişiklik izleme (tracking) ile AsNoTracking okuma maliyeti karşılaştırması (ölçüm).</summary>
[Trait(TestCategories.Key, TestCategories.Measurement)]
public sealed class TrackingComparisonTests : IClassFixture<ConcurrencyTestFactory>
{
    private readonly ConcurrencyTestFactory _factory;
    private readonly ITestOutputHelper _output;

    public TrackingComparisonTests(ConcurrencyTestFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    [Fact]
    public async Task Tracking_Ile_NoTracking_Performansini_Karsilastir()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScootlyDbContext>();
        var random = new Random(42);

        for (var i = 0; i < 5_000; i++)
            dbContext.Vehicles.Add(TestData.NewVehicle(40.0 + random.NextDouble() * 2, 28.0 + random.NextDouble() * 2, random.Next(1, 100)));

        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        var trackingStopwatch = Stopwatch.StartNew();
        var trackedResults = await dbContext.Vehicles.ToListAsync();
        trackingStopwatch.Stop();

        var noTrackingStopwatch = Stopwatch.StartNew();
        var untrackedResults = await dbContext.Vehicles.AsNoTracking().ToListAsync();
        noTrackingStopwatch.Stop();

        _output.WriteLine(
            $"Tracking ile {trackingStopwatch.ElapsedMilliseconds} ms ({trackedResults.Count} kayıt), " +
            $"AsNoTracking ile {noTrackingStopwatch.ElapsedMilliseconds} ms ({untrackedResults.Count} kayıt)");

        Assert.Equal(trackedResults.Count, untrackedResults.Count);
    }
}
