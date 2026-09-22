using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scootly.Domain.Fleet;
using Scootly.Domain.Geo;
using Scootly.Domain.Riding;
using Scootly.Infrastructure.Persistence;
using Xunit;
using Xunit.Abstractions;

namespace Scootly.Concurrency.Tests;

public sealed class NPlusOneTests : IClassFixture<ConcurrencyTestFactory>
{
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

        await SeedRidesWithVehiclesAsync(dbContext, count: 10);

        QueryCounter.Reset();

        var rides = await dbContext.Rides.ToListAsync();

        foreach (var ride in rides)
        {
            var vehicle = await dbContext.Vehicles.FirstOrDefaultAsync(v => v.Id == ride.VehicleId);
            _output.WriteLine($"Ride {ride.Id} -> Vehicle {vehicle?.Model.Brand}");
        }

        throw new Xunit.Sdk.XunitException(
            $"KÖTÜ desen — 10 Ride için toplam sorgu sayısı: {QueryCounter.Count} (beklenen: 11)");
    }

    [Fact]
    public async Task Iyi_Pattern_Tek_Sorguda_Vehicle_Bilgisini_Getirir()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScootlyDbContext>();

        await SeedRidesWithVehiclesAsync(dbContext, count: 10);

        QueryCounter.Reset();

        var results = await dbContext.Rides
            .Select(ride => new
            {
                RideId = ride.Id,
                Brand = dbContext.Vehicles
                    .Where(v => v.Id == ride.VehicleId)
                    .Select(v => v.Model.Brand)
                    .FirstOrDefault()
            })
            .ToListAsync();

        foreach (var result in results)
        {
            _output.WriteLine($"Ride {result.RideId} -> Vehicle {result.Brand}");
        }

        throw new Xunit.Sdk.XunitException(
            $"İYİ desen — 10 Ride için toplam sorgu sayısı: {QueryCounter.Count} (beklenen: 1)");
    }

    private async Task SeedRidesWithVehiclesAsync(ScootlyDbContext dbContext, int count)
    {
        for (var i = 0; i < count; i++)
        {
            var vehicle = new Vehicle(
                VehicleId.New(),
                new VehicleModel("Xiaomi", 25),
                new GeoPoint(41.0, 29.0),
                new BatteryLevel(80));

            dbContext.Vehicles.Add(vehicle);

            var ride = new Ride(
                RideId.New(),
                Guid.NewGuid(),
                vehicle.Id,
                new GeoPoint(41.0, 29.0),
                DateTime.UtcNow);

            dbContext.Rides.Add(ride);
        }

        await dbContext.SaveChangesAsync();
    }
}