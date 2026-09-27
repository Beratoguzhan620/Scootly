using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scootly.Infrastructure.Persistence;
using Scootly.Testing;
using Xunit;

namespace Scootly.Concurrency.Tests;

/// <summary>Sorgu sayacı süreç genelinde paylaşıldığı için bu testler diğerleriyle paralel çalışmaz.</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class QueryCountingCollection
{
    public const string Name = "query-counting";
}

/// <summary>ADR 0008: N+1 sorgu deseninin tespiti ve önlenmesi.</summary>
[Collection(QueryCountingCollection.Name)]
public sealed class NPlusOneTests : IClassFixture<ConcurrencyTestFactory>
{
    private const int RideCount = 10;

    private readonly ConcurrencyTestFactory _factory;
    private readonly ITestOutputHelper _output;

    public NPlusOneTests(ConcurrencyTestFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    [Fact]
    public async Task Kotu_Pattern_Her_Ride_Icin_Ayri_Vehicle_Sorgusu_Atar()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScootlyDbContext>();
        var rideIds = await SeedRidesWithVehiclesAsync(dbContext);

        QueryCounter.Reset();

        var rides = await dbContext.Rides.Where(r => rideIds.Contains(r.Id)).ToListAsync();

        foreach (var ride in rides)
            await dbContext.Vehicles.FirstOrDefaultAsync(v => v.Id == ride.VehicleId);

        _output.WriteLine($"KÖTÜ desen: {RideCount} sürüş için sorgu sayısı {QueryCounter.Count}");
        Assert.Equal(RideCount + 1, QueryCounter.Count);
    }

    [Fact]
    public async Task Iyi_Pattern_Tek_Sorguda_Vehicle_Bilgisini_Getirir()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScootlyDbContext>();
        var rideIds = await SeedRidesWithVehiclesAsync(dbContext);

        QueryCounter.Reset();

        var results = await dbContext.Rides
            .Where(r => rideIds.Contains(r.Id))
            .Select(ride => new
            {
                RideId = ride.Id,
                Brand = dbContext.Vehicles
                    .Where(v => v.Id == ride.VehicleId)
                    .Select(v => v.Model.Brand)
                    .FirstOrDefault()
            })
            .ToListAsync();

        _output.WriteLine($"İYİ desen: {results.Count} sürüş için sorgu sayısı {QueryCounter.Count}");
        Assert.Equal(1, QueryCounter.Count);
        Assert.All(results, r => Assert.NotNull(r.Brand));
    }

    private static async Task<List<Guid>> SeedRidesWithVehiclesAsync(ScootlyDbContext dbContext)
    {
        var rideIds = new List<Guid>();

        for (var i = 0; i < RideCount; i++)
        {
            var vehicle = TestData.NewVehicle();
            dbContext.Vehicles.Add(vehicle);

            var ride = TestData.NewActiveRide(Guid.NewGuid(), vehicle.Id);
            dbContext.Rides.Add(ride);
            rideIds.Add(ride.Id);
        }

        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        return rideIds;
    }
}
