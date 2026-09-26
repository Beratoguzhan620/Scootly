using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scootly.Domain.Fleet;
using Scootly.Domain.Geo;
using Scootly.Infrastructure.Persistence;
using Xunit;

namespace Scootly.Concurrency.Tests;

public sealed class AsyncMisuseTests : IClassFixture<ConcurrencyTestFactory>
{
    private readonly ConcurrencyTestFactory _factory;

    public AsyncMisuseTests(ConcurrencyTestFactory factory)
    {
        _factory = factory;
    }

#pragma warning disable xUnit1031
    [Fact]
    public void Yanlis_Kullanim_Result_Ile_Senkron_Cagirma_Yavas_Olabilir()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScootlyDbContext>();

        var vehicle = new Vehicle(
            VehicleId.New(),
            new VehicleModel("Xiaomi", 25),
            new GeoPoint(41.0, 29.0),
            new BatteryLevel(80));

        dbContext.Vehicles.Add(vehicle);
        dbContext.SaveChangesAsync().GetAwaiter().GetResult();

        var tasks = Enumerable.Range(0, 30).Select(_ => Task.Run(() =>
        {
            using var innerScope = _factory.Services.CreateScope();
            var innerDbContext = innerScope.ServiceProvider.GetRequiredService<ScootlyDbContext>();

            // YANLIŞ KULLANIM: .Result ile async metodu senkron çağırıyoruz — iş parçacığını bloklar
            var result = EntityFrameworkQueryableExtensions.CountAsync(innerDbContext.Vehicles).Result;
            return result;
        }));

        var stopwatch = Stopwatch.StartNew();
        Task.WaitAll(tasks.ToArray());
        stopwatch.Stop();

        throw new Xunit.Sdk.XunitException(
            $"YANLIŞ (.Result ile senkron bloklama): 30 paralel çağrı süresi: {stopwatch.ElapsedMilliseconds} ms");
    }
#pragma warning restore xUnit1031

    [Fact]
    public async Task Dogru_Kullanim_Await_Ile_Cagirma()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScootlyDbContext>();

        var vehicle = new Vehicle(
            VehicleId.New(),
            new VehicleModel("Xiaomi", 25),
            new GeoPoint(41.0, 29.0),
            new BatteryLevel(80));

        dbContext.Vehicles.Add(vehicle);
        await dbContext.SaveChangesAsync();

        var tasks = Enumerable.Range(0, 30).Select(async _ =>
        {
            using var innerScope = _factory.Services.CreateScope();
            var innerDbContext = innerScope.ServiceProvider.GetRequiredService<ScootlyDbContext>();

            // DOĞRU KULLANIM: await ile iş parçacığı bloklanmaz, havuza geri döner
            return await EntityFrameworkQueryableExtensions.CountAsync(innerDbContext.Vehicles);
        });

        var stopwatch = Stopwatch.StartNew();
        await Task.WhenAll(tasks);
        stopwatch.Stop();

        throw new Xunit.Sdk.XunitException(
            $"DOĞRU (await ile): 30 paralel çağrı süresi: {stopwatch.ElapsedMilliseconds} ms");
    }
}