using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scootly.Domain.Fleet;
using Scootly.Infrastructure.Persistence;
using Scootly.Testing;
using Xunit;

namespace Scootly.Concurrency.Tests;

/// <summary>ADR 0009: tekil INSERT ile PostgreSQL COPY BINARY karşılaştırması (ölçüm).</summary>
[Trait(TestCategories.Key, TestCategories.Measurement)]
public sealed class BulkInsertComparisonTests : IClassFixture<ConcurrencyTestFactory>
{
    private const int RecordCount = 5_000;

    private readonly ConcurrencyTestFactory _factory;
    private readonly ITestOutputHelper _output;

    public BulkInsertComparisonTests(ConcurrencyTestFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    [Fact]
    public async Task Tekil_Insert_Ile_Toplu_Insert_Performansini_Karsilastir()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScootlyDbContext>();
        var random = new Random(42);

        var individualStopwatch = Stopwatch.StartNew();

        for (var i = 0; i < RecordCount; i++)
            dbContext.Vehicles.Add(TestData.NewVehicle(40.0 + random.NextDouble() * 2, 28.0 + random.NextDouble() * 2, random.Next(1, 100)));

        await dbContext.SaveChangesAsync();
        individualStopwatch.Stop();

        var bulkVehicles = new List<Vehicle>(RecordCount);

        for (var i = 0; i < RecordCount; i++)
            bulkVehicles.Add(TestData.NewVehicle(40.0 + random.NextDouble() * 2, 28.0 + random.NextDouble() * 2, random.Next(1, 100), "Segway"));

        var bulkStopwatch = Stopwatch.StartNew();
        await BulkVehicleSeeder.BulkInsertAsync(dbContext.Database.GetConnectionString()!, bulkVehicles);
        bulkStopwatch.Stop();

        _output.WriteLine(
            $"{RecordCount} kayıt: tekil INSERT (SaveChangesAsync) {individualStopwatch.ElapsedMilliseconds} ms, " +
            $"toplu INSERT (COPY BINARY) {bulkStopwatch.ElapsedMilliseconds} ms");

        var bulkIds = bulkVehicles.Select(b => b.Id).ToList();
        var bulkInserted = await dbContext.Vehicles.CountAsync(v => bulkIds.Contains(v.Id));
        Assert.Equal(RecordCount, bulkInserted);
    }
}
