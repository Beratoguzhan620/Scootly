using System.Net;
using System.Net.Http.Json;
using Scootly.Api.Contracts.Responses;
using Scootly.Infrastructure.Identity;
using Scootly.Testing;
using Xunit;

namespace Scootly.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class VehicleEndpointsTests
{
    private readonly ScootlyApiFactory _factory;

    public VehicleEndpointsTests(ScootlyApiFactory factory)
    {
        _factory = factory;
    }

    private static object ValidVehicle => new
    {
        Brand = "Segway",
        RangeKm = 30,
        Latitude = 41.0,
        Longitude = 29.0,
        BatteryPercentage = 90
    };

    [Fact]
    public async Task Filo_Yoneticisi_Arac_Kaydedebilmeli_Ve_Arac_Kalici_Olmali()
    {
        var manager = await _factory.CreateUserClientAsync(ScootlyRoles.FleetManager);

        var response = await manager.Client.PostAsJsonAsync("/api/v1/vehicles", ValidVehicle);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<RegisterVehicleResponse>();

        var fetched = await _factory.CreateClient().GetAsync(response.Headers.Location);
        Assert.Equal(HttpStatusCode.OK, fetched.StatusCode);

        var vehicle = await fetched.Content.ReadFromJsonAsync<VehicleResponseV2>();
        Assert.Equal(created!.Id, vehicle!.Id);
        Assert.Equal("Segway", vehicle.Brand);
    }

    [Fact]
    public async Task Surucu_Arac_Kaydedemez()
    {
        var driver = await _factory.CreateDriverClientAsync();

        var response = await driver.Client.PostAsJsonAsync("/api/v1/vehicles", ValidVehicle);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Anonim_Kullanici_Arac_Kaydedemez()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/v1/vehicles", ValidVehicle);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Gecersiz_Arac_Bilgisi_400_Donmeli()
    {
        var manager = await _factory.CreateUserClientAsync(ScootlyRoles.FleetManager);

        var response = await manager.Client.PostAsJsonAsync("/api/v1/vehicles", new
        {
            Brand = "",
            RangeKm = 30,
            Latitude = 41.0,
            Longitude = 29.0,
            BatteryPercentage = 150
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Konum_Filtresi_Yalnizca_Belirtilen_Bolgedeki_Araclari_Dondurmeli()
    {
        await _factory.SeedVehicleAsync(41.0, 29.0);
        await _factory.SeedVehicleAsync(50.0, 40.0);

        var response = await _factory.CreateClient().GetFromJsonAsync<PagedResult<VehicleResponse>>(
            "/api/v1/vehicles?minLatitude=40&maxLatitude=42&minLongitude=28&maxLongitude=30&pageSize=100");

        Assert.NotEmpty(response!.Items);
        Assert.All(response.Items, v => Assert.InRange(v.Latitude, 40, 42));
    }

    [Theory]
    [InlineData("pageNumber=0")]
    [InlineData("pageSize=101")]
    [InlineData("pageNumber=2147483647")]
    [InlineData("minLatitude=42&maxLatitude=40")]
    [InlineData("minLongitude=-181")]
    public async Task Gecersiz_Sorgu_400_Donmeli(string query)
    {
        var response = await _factory.CreateClient().GetAsync($"/api/v1/vehicles?{query}");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task V2_Liste_Model_Bilgisi_Icermeli()
    {
        await _factory.SeedVehicleAsync();

        var response = await _factory.CreateClient().GetFromJsonAsync<PagedResult<VehicleResponseV2>>("/api/v2/vehicles?pageSize=5");

        Assert.NotEmpty(response!.Items);
        Assert.All(response.Items, v => Assert.False(string.IsNullOrEmpty(v.Brand)));
    }

    [Fact]
    public async Task Saha_Operatoru_Araci_Bakima_Alip_Hizmete_Dondurebilmeli()
    {
        var vehicle = await _factory.SeedVehicleAsync();
        var operatorClient = await _factory.CreateUserClientAsync(ScootlyRoles.FieldOperator);

        var maintenance = await operatorClient.Client.PostAsync($"/api/v1/vehicles/{vehicle.Id}/maintenance", null);
        Assert.Equal(HttpStatusCode.NoContent, maintenance.StatusCode);

        var reserveWhileInMaintenance = await (await _factory.CreateDriverClientAsync()).Client
            .PostAsync($"/api/v1/vehicles/{vehicle.Id}/reserve", null);
        Assert.Equal(HttpStatusCode.Conflict, reserveWhileInMaintenance.StatusCode);

        var back = await operatorClient.Client.PostAsync($"/api/v1/vehicles/{vehicle.Id}/return-to-service", null);
        Assert.Equal(HttpStatusCode.NoContent, back.StatusCode);
    }

    [Fact]
    public async Task Surucu_Araci_Bakima_Alamaz()
    {
        var vehicle = await _factory.SeedVehicleAsync();
        var driver = await _factory.CreateDriverClientAsync();

        var response = await driver.Client.PostAsync($"/api/v1/vehicles/{vehicle.Id}/maintenance", null);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Olmayan_Arac_404_Donmeli()
    {
        var response = await _factory.CreateClient().GetAsync($"/api/v1/vehicles/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
