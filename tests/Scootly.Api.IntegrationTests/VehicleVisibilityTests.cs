using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Scootly.Api.Contracts.Responses;
using Scootly.Domain.Fleet;
using Scootly.Infrastructure.Identity;
using Scootly.Testing;
using Xunit;

namespace Scootly.Api.IntegrationTests;

/// <summary>
/// Konum gizliliği: müsait olmayan (rezerve, sürüşte, bakımda, kayıp) araçların konumu yalnızca filo ekibine,
/// cihaz ağ geçidine ve aracı kullanan sürücüye görünür.
/// </summary>
[Collection(ApiCollection.Name)]
public sealed class VehicleVisibilityTests
{
    private readonly ScootlyApiFactory _factory;

    public VehicleVisibilityTests(ScootlyApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Anonim_Liste_Musait_Olmayan_Araclari_Gostermemeli()
    {
        var (reserved, _) = await SeedReservedVehicleAsync();

        var v1 = await _factory.CreateClient().GetFromJsonAsync<PagedResult<VehicleResponse>>(BoundingBoxQuery(reserved, version: 1));
        var v2 = await _factory.CreateClient().GetFromJsonAsync<PagedResult<VehicleResponseV2>>(BoundingBoxQuery(reserved, version: 2));

        Assert.DoesNotContain(v1!.Items, v => v.Id == reserved.Id);
        Assert.DoesNotContain(v2!.Items, v => v.Id == reserved.Id);
        Assert.All(v1.Items, v => Assert.Equal(nameof(VehicleStatus.Available), v.Status));
    }

    [Fact]
    public async Task Filo_Ekibi_Ve_Cihaz_Tum_Araclari_Gorebilmeli()
    {
        var (reserved, _) = await SeedReservedVehicleAsync();
        var fieldOperator = await _factory.CreateUserClientAsync(ScootlyRoles.FieldOperator);
        var device = await _factory.CreateDeviceClientAsync();

        var byOperator = await fieldOperator.Client.GetFromJsonAsync<PagedResult<VehicleResponse>>(BoundingBoxQuery(reserved, version: 1));
        var byDevice = await device.GetFromJsonAsync<PagedResult<VehicleResponseV2>>(BoundingBoxQuery(reserved, version: 2));

        Assert.Contains(byOperator!.Items, v => v.Id == reserved.Id && v.Status == nameof(VehicleStatus.Reserved));
        Assert.Contains(byDevice!.Items, v => v.Id == reserved.Id);
    }

    [Fact]
    public async Task Musait_Olmayan_Arac_Ayrintisi_Yalnizca_Ilgili_Surucuye_Gorunmeli()
    {
        var (reserved, owner) = await SeedReservedVehicleAsync();
        var otherDriver = await _factory.CreateDriverClientAsync();

        var anonymous = await _factory.CreateClient().GetAsync($"/api/v1/vehicles/{reserved.Id}");
        var byOther = await otherDriver.Client.GetAsync($"/api/v1/vehicles/{reserved.Id}");
        var byOwner = await owner.Client.GetAsync($"/api/v1/vehicles/{reserved.Id}");

        Assert.Equal(HttpStatusCode.NotFound, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, byOther.StatusCode);
        Assert.Equal(HttpStatusCode.OK, byOwner.StatusCode);
    }

    [Fact]
    public async Task V2_Liste_Konum_Filtresini_Uygulamali()
    {
        var inside = await _factory.SeedVehicleAsync(38.4, 27.1);
        var outside = await _factory.SeedVehicleAsync(39.9, 32.8);

        var page = await _factory.CreateClient().GetFromJsonAsync<PagedResult<VehicleResponseV2>>(
            "/api/v2/vehicles?minLatitude=38.3&maxLatitude=38.5&minLongitude=27.0&maxLongitude=27.2&pageSize=100");

        Assert.Contains(page!.Items, v => v.Id == inside.Id);
        Assert.DoesNotContain(page.Items, v => v.Id == outside.Id);
    }

    [Fact]
    public async Task Saha_Operatoru_Araci_Kayip_Isaretleyip_Geri_Getirebilmeli()
    {
        var vehicle = await _factory.SeedVehicleAsync();
        var fieldOperator = await _factory.CreateUserClientAsync(ScootlyRoles.FieldOperator);
        var driver = await _factory.CreateDriverClientAsync();

        var lost = await fieldOperator.Client.PostAsync($"/api/v1/vehicles/{vehicle.Id}/lost", null);
        var lostAgain = await fieldOperator.Client.PostAsync($"/api/v1/vehicles/{vehicle.Id}/lost", null);
        var byDriver = await driver.Client.PostAsync($"/api/v1/vehicles/{vehicle.Id}/lost", null);
        var reserveLost = await driver.Client.PostAsync($"/api/v1/vehicles/{vehicle.Id}/reserve", null);

        Assert.Equal(HttpStatusCode.NoContent, lost.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, lostAgain.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byDriver.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, reserveLost.StatusCode);

        var back = await fieldOperator.Client.PostAsync($"/api/v1/vehicles/{vehicle.Id}/return-to-service", null);
        Assert.Equal(HttpStatusCode.NoContent, back.StatusCode);
    }

    [Fact]
    public async Task Bataryasi_Cok_Dusuk_Arac_Rezerve_Edilemez()
    {
        var vehicle = await _factory.SeedVehicleAsync(batteryPercentage: BatteryLevel.MinimumRentablePercentage - 1);
        var driver = await _factory.CreateDriverClientAsync();

        var response = await driver.Client.PostAsync($"/api/v1/vehicles/{vehicle.Id}/reserve", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var stored = await _factory.WithDbContextAsync(db => db.Vehicles.AsNoTracking().SingleAsync(v => v.Id == vehicle.Id));
        Assert.Equal(VehicleStatus.Available, stored.Status);
    }

    private async Task<(Vehicle Vehicle, AuthenticatedClient Owner)> SeedReservedVehicleAsync()
    {
        // Her test kendi küçük alanını kullanır: başka testlerin araçları sonucu etkilemez.
        var latitude = 36 + Random.Shared.NextDouble() * 5;
        var longitude = 26 + Random.Shared.NextDouble() * 10;

        var vehicle = await _factory.SeedVehicleAsync(latitude, longitude);
        var owner = await _factory.CreateDriverClientAsync();

        var reserve = await owner.Client.PostAsync($"/api/v1/vehicles/{vehicle.Id}/reserve", null);
        reserve.EnsureSuccessStatusCode();

        return (vehicle, owner);
    }

    private static string BoundingBoxQuery(Vehicle vehicle, int version)
    {
        var lat = vehicle.Location.Latitude;
        var lon = vehicle.Location.Longitude;

        return FormattableString.Invariant(
            $"/api/v{version}/vehicles?minLatitude={lat - 0.0001}&maxLatitude={lat + 0.0001}&minLongitude={lon - 0.0001}&maxLongitude={lon + 0.0001}&pageSize=100");
    }
}
