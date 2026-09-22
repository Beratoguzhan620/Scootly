using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scootly.Domain.Fleet;
using Scootly.Domain.Geo;
using Scootly.Infrastructure.Persistence;
using System.Diagnostics;
using Xunit;

namespace Scootly.Concurrency.Tests;

public sealed class BulkInsertComparisonTests : IClassFixture<ConcurrencyTestFactory>
{
    private readonly ConcurrencyTestFactory _factory;

    public BulkInsertComparisonTests(ConcurrencyTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Tekil_Insert_Ile_Toplu_Insert_Performansini_Karsilastir()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScootlyDbContext>();

        var random = new Random(42);

        // --- Tekil INSERT (EF Core varsayılan davranışı) ---
        var individualStopwatch = Stopwatch.StartNew();

        for (var i = 0; i < 5_000; i++)
        {
            dbContext.Vehicles.Add(new Vehicle(
                VehicleId.New(),
                new VehicleModel("Xiaomi", 25),
                new GeoPoint(40.0 + random.NextDouble() * 2, 28.0 + random.NextDouble() * 2),
                new BatteryLevel(random.Next(1, 100))));
        }

        await dbContext.SaveChangesAsync();
        individualStopwatch.Stop();

        // --- Toplu INSERT (COPY BINARY) ---
        var bulkVehicles = new List<Vehicle>();

        for (var i = 0; i < 5_000; i++)
        {
            bulkVehicles.Add(new Vehicle(
                VehicleId.New(),
                new VehicleModel("Segway", 30),
                new GeoPoint(40.0 + random.NextDouble() * 2, 28.0 + random.NextDouble() * 2),
                new BatteryLevel(random.Next(1, 100))));
        }

        var connectionString = dbContext.Database.GetConnectionString()!;

        var bulkStopwatch = Stopwatch.StartNew();
        await BulkVehicleSeeder.BulkInsertAsync(connectionString, bulkVehicles);
        bulkStopwatch.Stop();

        throw new Xunit.Sdk.XunitException(
            $"5.000 kayıt — Tekil INSERT (SaveChangesAsync): {individualStopwatch.ElapsedMilliseconds} ms | " +
            $"Toplu INSERT (COPY BINARY): {bulkStopwatch.ElapsedMilliseconds} ms");
    }
}