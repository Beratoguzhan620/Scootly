using System.Net;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Scootly.Api.Contracts.Responses;
using Xunit;

namespace Scootly.Api.IntegrationTests;

public sealed class RideEndpointsTests : IClassFixture<ScootlyApiFactory>
{
    private readonly ScootlyApiFactory _factory;

    public RideEndpointsTests(ScootlyApiFactory factory)
    {
        _factory = factory;
    }

    /// <remarks>
    /// <para>
    /// <b>60. gün — bu test BAYAT ve atlanıyor.</b> İki ayrı sebeple:
    /// </para>
    /// <list type="number">
    ///   <item>Testcontainers bu geliştirme ortamında çalışmıyor: Apple
    ///   Silicon üzerinde Parallels ile çalışan bir Windows misafirinde
    ///   Docker'a erişim yok (bkz. tests/Scootly.DbLab/Lab.cs).</item>
    ///   <item>Sınadığı akış artık yok: 26. günde <c>DriverId</c> gövdeden
    ///   kaldırıldı ve uçlar kimlik doğrulaması istiyor, 42. günde ise
    ///   <c>/api/vehicles</c> enlem/boylam zorunlu hale geldi.</item>
    /// </list>
    /// <para>
    /// SİLİNMEDİ çünkü silmek, uçtan uca testin hiç var olmadığını
    /// düşündürürdü. Atlandığı ve NEDEN atlandığı görünür duruyor; teknik
    /// borç listesinde de kayıtlı.
    /// </para>
    /// </remarks>
    [Fact(Skip = "Bayat: Testcontainers bu ortamda calismiyor ve sinanan akis 26/42. gunlerde degisti.")]
    public async Task Kirala_Baslat_Bitir_Akisi_Uctan_Uca_Calismali()
    {
        var client = _factory.CreateClient();

        var vehicleId = await SeedVehicleAsync();
        var driverId = Guid.NewGuid();

        var reserveResponse = await client.PostAsJsonAsync(
            $"/api/vehicles/{vehicleId}/reserve",
            new { DriverId = driverId });

        Assert.Equal(HttpStatusCode.OK, reserveResponse.StatusCode);

        var startResponse = await client.PostAsJsonAsync(
            "/api/rides/start",
            new { VehicleId = vehicleId, DriverId = driverId });

        Assert.Equal(HttpStatusCode.OK, startResponse.StatusCode);

        var vehiclesResponse = await client.GetFromJsonAsync<List<VehicleResponse>>("/api/vehicles");
        var activeVehicle = vehiclesResponse!.First(v => v.Id == vehicleId);
        Assert.Equal("InRide", activeVehicle.Status);
    }

    private async Task<Guid> SeedVehicleAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<Scootly.Infrastructure.Persistence.ScootlyDbContext>();

        var vehicle = new Scootly.Domain.Fleet.Vehicle(
            Scootly.Domain.Fleet.VehicleId.New(),
            new Scootly.Domain.Fleet.VehicleModel("Xiaomi", 25),
            new Scootly.Domain.Geo.GeoPoint(41.0, 29.0),
            new Scootly.Domain.Fleet.BatteryLevel(80));

        dbContext.Vehicles.Add(vehicle);
        await dbContext.SaveChangesAsync();

        return vehicle.Id;
    }
}