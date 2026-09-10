using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scootly.Domain.Fleet;
using Scootly.Domain.Geo;
using Scootly.Infrastructure.Persistence;
using Xunit;

namespace Scootly.Concurrency.Tests;

public sealed class DeadlockTests : IClassFixture<ConcurrencyTestFactory>
{
    private readonly ConcurrencyTestFactory _factory;

    public DeadlockTests(ConcurrencyTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Ters_Sirayla_Kilitleme_Deadlock_Uretmeli()
    {
        var (vehicle1Id, vehicle2Id) = await SeedTwoVehiclesAsync();

        var taskA = LockInOrderAsync(vehicle1Id, vehicle2Id, delayMs: 200);
        var taskB = LockInOrderAsync(vehicle2Id, vehicle1Id, delayMs: 200);

        var results = await Task.WhenAll(taskA, taskB);

        var deadlockDetected = results.Any(r => r.Contains("deadlock", StringComparison.OrdinalIgnoreCase));

        throw new Xunit.Sdk.XunitException(
            $"A sonucu: {results[0]} | B sonucu: {results[1]} | Deadlock tespit edildi mi: {deadlockDetected}");
    }

    [Fact]
    public async Task Ayni_Sirayla_Kilitleme_Deadlock_Uretmemeli()
    {
        var (vehicle1Id, vehicle2Id) = await SeedTwoVehiclesAsync();

        // İki görev de ARTIK AYNI SIRAYLA kilitliyor: önce vehicle1, sonra vehicle2
        var taskA = LockInOrderAsync(vehicle1Id, vehicle2Id, delayMs: 200);
        var taskB = LockInOrderAsync(vehicle1Id, vehicle2Id, delayMs: 200);

        var results = await Task.WhenAll(taskA, taskB);

        var deadlockDetected = results.Any(r => r.Contains("deadlock", StringComparison.OrdinalIgnoreCase));

        throw new Xunit.Sdk.XunitException(
            $"A sonucu: {results[0]} | B sonucu: {results[1]} | Deadlock tespit edildi mi: {deadlockDetected}");
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
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScootlyDbContext>();

        var vehicle1 = new Vehicle(VehicleId.New(), new VehicleModel("Xiaomi", 25), new GeoPoint(41.0, 29.0), new BatteryLevel(80));
        var vehicle2 = new Vehicle(VehicleId.New(), new VehicleModel("Segway", 30), new GeoPoint(41.1, 29.1), new BatteryLevel(90));

        dbContext.Vehicles.Add(vehicle1);
        dbContext.Vehicles.Add(vehicle2);
        await dbContext.SaveChangesAsync();

        return (vehicle1.Id, vehicle2.Id);
    }
}