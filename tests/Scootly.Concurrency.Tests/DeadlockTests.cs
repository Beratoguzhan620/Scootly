using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scootly.Infrastructure.Persistence;
using Xunit;

namespace Scootly.Concurrency.Tests;

/// <summary>ADR 0005: kilitlerin tutarlı sırayla alınması deadlock'u önler.</summary>
public sealed class DeadlockTests : IClassFixture<ConcurrencyTestFactory>
{
    private readonly ConcurrencyTestFactory _factory;
    private readonly ITestOutputHelper _output;

    public DeadlockTests(ConcurrencyTestFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    [Fact]
    public async Task Ters_Sirayla_Kilitleme_Deadlock_Uretmeli()
    {
        var (vehicle1Id, vehicle2Id) = await SeedTwoVehiclesAsync();

        var results = await Task.WhenAll(
            LockInOrderAsync(vehicle1Id, vehicle2Id, delayMs: 200),
            LockInOrderAsync(vehicle2Id, vehicle1Id, delayMs: 200));

        _output.WriteLine($"A: {results[0]} | B: {results[1]}");

        Assert.Contains(results, r => r.Contains("deadlock", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Ayni_Sirayla_Kilitleme_Deadlock_Uretmemeli()
    {
        var (vehicle1Id, vehicle2Id) = await SeedTwoVehiclesAsync();

        // İki görev de aynı sırayla kilitliyor: önce vehicle1, sonra vehicle2.
        var results = await Task.WhenAll(
            LockInOrderAsync(vehicle1Id, vehicle2Id, delayMs: 200),
            LockInOrderAsync(vehicle1Id, vehicle2Id, delayMs: 200));

        _output.WriteLine($"A: {results[0]} | B: {results[1]}");

        Assert.All(results, r => Assert.Equal("Başarılı", r));
    }

    private async Task<string> LockInOrderAsync(Guid firstId, Guid secondId, int delayMs)
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScootlyDbContext>();

        await using var transaction = await dbContext.Database.BeginTransactionAsync();

        try
        {
            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT * FROM \"Vehicles\" WHERE \"Id\" = {firstId} FOR UPDATE");

            await Task.Delay(delayMs);

            await dbContext.Database.ExecuteSqlInterpolatedAsync(
                $"SELECT * FROM \"Vehicles\" WHERE \"Id\" = {secondId} FOR UPDATE");

            await transaction.CommitAsync();
            return "Başarılı";
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync();
            return $"Hata: {ex.Message}";
        }
    }

    private async Task<(Guid, Guid)> SeedTwoVehiclesAsync()
    {
        var vehicle1 = await _factory.SeedVehicleAsync(41.0, 29.0);
        var vehicle2 = await _factory.SeedVehicleAsync(41.1, 29.1);

        return (vehicle1.Id, vehicle2.Id);
    }
}
