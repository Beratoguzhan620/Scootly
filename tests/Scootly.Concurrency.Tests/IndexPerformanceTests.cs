using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scootly.Domain.Fleet;
using Scootly.Domain.Geo;
using Scootly.Infrastructure.Persistence;
using Xunit;

namespace Scootly.Concurrency.Tests;

public sealed class IndexPerformanceTests : IClassFixture<ConcurrencyTestFactory>
{
    private readonly ConcurrencyTestFactory _factory;

    public IndexPerformanceTests(ConcurrencyTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Bolge_Sorgusu_Performansini_Olc()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScootlyDbContext>();

        var random = new Random(42);

        for (var i = 0; i < 10_000; i++)
        {
            var vehicle = new Vehicle(
                VehicleId.New(),
                new VehicleModel("Xiaomi", 25),
                new GeoPoint(
                    40.0 + random.NextDouble() * 2,
                    28.0 + random.NextDouble() * 2),
                new BatteryLevel(random.Next(1, 100)));

            dbContext.Vehicles.Add(vehicle);
        }

        await dbContext.SaveChangesAsync();

        var stopwatch = Stopwatch.StartNew();

        var results = await dbContext.Vehicles
            .AsNoTracking()
            .Where(v => v.Location.Latitude > 40.5 && v.Location.Latitude < 41.5)
            .Where(v => v.Location.Longitude > 28.5 && v.Location.Longitude < 29.5)
            .Where(v => v.Status == VehicleStatus.Available)
            .ToListAsync();

        stopwatch.Stop();

        throw new Xunit.Sdk.XunitException(
            $"10.000 araç arasında bölge sorgusu süresi (AsNoTracking): {stopwatch.ElapsedMilliseconds} ms | Bulunan: {results.Count}");
    }

    [Fact]
    public async Task Tek_Id_Ile_Arama_Cok_Hizli_Olmali()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScootlyDbContext>();

        var random = new Random(42);
        Guid targetId = Guid.Empty;

        for (var i = 0; i < 10_000; i++)
        {
            var vehicle = new Vehicle(
                VehicleId.New(),
                new VehicleModel("Xiaomi", 25),
                new GeoPoint(
                    40.0 + random.NextDouble() * 2,
                    28.0 + random.NextDouble() * 2),
                new BatteryLevel(random.Next(1, 100)));

            dbContext.Vehicles.Add(vehicle);

            if (i == 5000)
                targetId = vehicle.Id;
        }

        await dbContext.SaveChangesAsync();

        var stopwatch = Stopwatch.StartNew();

        var result = await dbContext.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.Id == targetId);

        stopwatch.Stop();

        throw new Xunit.Sdk.XunitException(
            $"10.000 araç arasında Id ile arama süresi (AsNoTracking): {stopwatch.ElapsedMilliseconds} ms | Bulundu: {result is not null}");
    }

    [Fact]
    public async Task Tek_Id_Ile_Arama_Adim_Adim_Olc()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScootlyDbContext>();

        var random = new Random(42);
        Guid targetId = Guid.Empty;

        var loopStopwatch = Stopwatch.StartNew();

        for (var i = 0; i < 10_000; i++)
        {
            var vehicle = new Vehicle(
                VehicleId.New(),
                new VehicleModel("Xiaomi", 25),
                new GeoPoint(
                    40.0 + random.NextDouble() * 2,
                    28.0 + random.NextDouble() * 2),
                new BatteryLevel(random.Next(1, 100)));

            dbContext.Vehicles.Add(vehicle);

            if (i == 5000)
                targetId = vehicle.Id;
        }

        loopStopwatch.Stop();

        var saveStopwatch = Stopwatch.StartNew();
        await dbContext.SaveChangesAsync();
        saveStopwatch.Stop();

        var firstQueryStopwatch = Stopwatch.StartNew();
        var result1 = await dbContext.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.Id == targetId);
        firstQueryStopwatch.Stop();

        var secondQueryStopwatch = Stopwatch.StartNew();
        var result2 = await dbContext.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.Id == targetId);
        secondQueryStopwatch.Stop();

        throw new Xunit.Sdk.XunitException(
            $"Döngü: {loopStopwatch.ElapsedMilliseconds} ms | " +
            $"SaveChanges: {saveStopwatch.ElapsedMilliseconds} ms | " +
            $"1. Sorgu (soğuk): {firstQueryStopwatch.ElapsedMilliseconds} ms | " +
            $"2. Sorgu (ısınmış): {secondQueryStopwatch.ElapsedMilliseconds} ms | " +
            $"Bulundu: {result1 is not null}, {result2 is not null}");
    }
}