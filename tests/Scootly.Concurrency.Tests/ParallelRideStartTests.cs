using System.Net;
using Xunit;

namespace Scootly.Concurrency.Tests;

public sealed class ParallelRideStartTests : IClassFixture<ConcurrencyTestFactory>
{
    private readonly ConcurrencyTestFactory _factory;

    public ParallelRideStartTests(ConcurrencyTestFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData(10)]
    [InlineData(50)]
    [InlineData(100)]
    public async Task Ayni_Araca_N_Paralel_Rezervasyon_Istegi_Yalnizca_Birini_Basarili_Kilmali(int concurrentRequestCount)
    {
        var vehicle = await _factory.SeedVehicleAsync();

        var drivers = await Task.WhenAll(Enumerable.Range(0, concurrentRequestCount).Select(_ => _factory.CreateDriverClientAsync()));

        var results = await Task.WhenAll(drivers.Select(async driver =>
            (await driver.Client.PostAsync($"/api/v1/vehicles/{vehicle.Id}/reserve", null)).StatusCode));

        Assert.Equal(1, results.Count(status => status == HttpStatusCode.OK));
        Assert.Equal(concurrentRequestCount - 1, results.Count(status => status == HttpStatusCode.Conflict));
    }

    [Fact]
    public async Task Ayni_Surucunun_Paralel_Rezervasyonlarindan_Yalnizca_Biri_Basarili_Olmali()
    {
        var driver = await _factory.CreateDriverClientAsync();
        var vehicles = await Task.WhenAll(Enumerable.Range(0, 10).Select(_ => _factory.SeedVehicleAsync()));

        var results = await Task.WhenAll(vehicles.Select(async vehicle =>
            (await driver.Client.PostAsync($"/api/v1/vehicles/{vehicle.Id}/reserve", null)).StatusCode));

        // Uygulama kontrolü yarışı kaçırsa bile veritabanındaki kısmi benzersiz indeks ikinci rezervasyonu engeller.
        Assert.Equal(1, results.Count(status => status == HttpStatusCode.OK));
    }
}
