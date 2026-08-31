using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;
using Xunit;

namespace Scootly.Api.IntegrationTests;

public sealed class RideEndpointsTests : IClassFixture<ScootlyApiFactory>
{
    private readonly ScootlyApiFactory _factory;

    public RideEndpointsTests(ScootlyApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
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

        var vehiclesResponse = await client.GetFromJsonAsync<List<VehicleDto>>("/api/vehicles");
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

    private sealed record VehicleDto(Guid Id, double Latitude, double Longitude, int BatteryPercentage, string Status);
}