using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Scootly.Api.Contracts.Responses;
using Scootly.Domain.Fleet;
using Scootly.Domain.Geo;
using Scootly.Infrastructure.Persistence;
using Xunit;

namespace Scootly.Api.IntegrationTests;

public sealed class VehicleFilteringTests : IClassFixture<ScootlyApiFactory>
{
    private readonly ScootlyApiFactory _factory;

    public VehicleFilteringTests(ScootlyApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Konum_Filtresi_Yalnizca_Belirtilen_Bolgedeki_Araclari_Dondurmeli()
    {
        var client = _factory.CreateClient();

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<ScootlyDbContext>();

            dbContext.Vehicles.Add(new Vehicle(
                VehicleId.New(), new VehicleModel("Xiaomi", 25),
                new GeoPoint(41.0, 29.0), new BatteryLevel(80)));

            dbContext.Vehicles.Add(new Vehicle(
                VehicleId.New(), new VehicleModel("Segway", 30),
                new GeoPoint(50.0, 40.0), new BatteryLevel(90)));

            await dbContext.SaveChangesAsync();
        }

        var response = await client.GetFromJsonAsync<PagedResult<VehicleResponse>>(
            "/api/v1/vehicles?minLatitude=40&maxLatitude=42&minLongitude=28&maxLongitude=30");

        Assert.All(response!.Items, v => Assert.InRange(v.Latitude, 40, 42));
    }
}