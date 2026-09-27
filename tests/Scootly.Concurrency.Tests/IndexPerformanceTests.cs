using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scootly.Domain.Fleet;
using Scootly.Infrastructure.Persistence;
using Scootly.Testing;
using Xunit;

namespace Scootly.Concurrency.Tests;

/// <summary>ADR 0006: bölge ve kimlik sorgularının maliyeti (ölçüm).</summary>
[Trait(TestCategories.Key, TestCategories.Measurement)]
public sealed class IndexPerformanceTests : IClassFixture<ConcurrencyTestFactory>
{
    private const int VehicleCount = 10_000;

    private readonly ConcurrencyTestFactory _factory;
    private readonly ITestOutputHelper _output;

    public IndexPerformanceTests(ConcurrencyTestFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    [Fact]
    public async Task Bolge_Sorgusu_Performansini_Olc()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScootlyDbContext>();
        await SeedAsync(dbContext);

        var stopwatch = Stopwatch.StartNew();

        var results = await dbContext.Vehicles
            .AsNoTracking()
            .Where(v => v.Location.Latitude > 40.5 && v.Location.Latitude < 41.5)
            .Where(v => v.Location.Longitude > 28.5 && v.Location.Longitude < 29.5)
            .Where(v => v.Status == VehicleStatus.Available)
            .ToListAsync();

        stopwatch.Stop();

        _output.WriteLine($"{VehicleCount} araç arasında bölge sorgusu {stopwatch.ElapsedMilliseconds} ms, bulunan {results.Count}");
        Assert.NotEmpty(results);
    }

    [Fact]
    public async Task Tek_Id_Ile_Arama_Adim_Adim_Olc()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScootlyDbContext>();

        var seedStopwatch = Stopwatch.StartNew();
        var targetId = await SeedAsync(dbContext);
        seedStopwatch.Stop();

        var coldStopwatch = Stopwatch.StartNew();
        var first = await dbContext.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.Id == targetId);
        coldStopwatch.Stop();

        var warmStopwatch = Stopwatch.StartNew();
        var second = await dbContext.Vehicles.AsNoTracking().FirstOrDefaultAsync(v => v.Id == targetId);
        warmStopwatch.Stop();

        _output.WriteLine(
            $"Tohumlama {seedStopwatch.ElapsedMilliseconds} ms, 1. sorgu (soğuk) {coldStopwatch.ElapsedMilliseconds} ms, " +
            $"2. sorgu (ısınmış) {warmStopwatch.ElapsedMilliseconds} ms");

        Assert.NotNull(first);
        Assert.NotNull(second);
    }

    private static async Task<Guid> SeedAsync(ScootlyDbContext dbContext)
    {
        var random = new Random(42);
        var targetId = Guid.Empty;

        for (var i = 0; i < VehicleCount; i++)
        {
            var vehicle = TestData.NewVehicle(40.0 + random.NextDouble() * 2, 28.0 + random.NextDouble() * 2, random.Next(1, 100));
            dbContext.Vehicles.Add(vehicle);

            if (i == VehicleCount / 2)
                targetId = vehicle.Id;
        }

        await dbContext.SaveChangesAsync();
        dbContext.ChangeTracker.Clear();

        return targetId;
    }
}
