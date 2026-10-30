using System.Net;
using System.Net.Http.Json;
using Scootly.Api.Contracts.Responses;
using Scootly.Domain.Riding;
using Scootly.Infrastructure.Identity;
using Scootly.Testing;
using Xunit;

namespace Scootly.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class ReservationEndpointsTests
{
    private const string ActiveReservationUrl = "/api/reservations/active";

    private readonly ScootlyApiFactory _factory;

    public ReservationEndpointsTests(ScootlyApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Rezervasyonu_Olmayan_Surucuye_204_Donmeli()
    {
        var driver = await _factory.CreateDriverClientAsync();

        var response = await driver.Client.GetAsync(ActiveReservationUrl);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Suren_Rezervasyon_Arac_Ve_Bitis_Zamaniyla_Donmeli()
    {
        var vehicle = await _factory.SeedVehicleAsync();
        var driver = await _factory.CreateDriverClientAsync();

        var reserve = await driver.Client.PostAsync($"/api/v1/vehicles/{vehicle.Id}/reserve", null);
        Assert.Equal(HttpStatusCode.OK, reserve.StatusCode);

        var reservation = (await driver.Client.GetFromJsonAsync<ActiveReservationResponse>(ActiveReservationUrl))!;

        Assert.Equal(vehicle.Id, reservation.VehicleId);
        Assert.Equal(vehicle.Id, reservation.Vehicle.Id);
        Assert.Equal("Reserved", reservation.Vehicle.Status);
        Assert.Equal(ReservationPolicy.Duration, reservation.ExpiresAt - reservation.ReservedAt);
        Assert.True(reservation.ExpiresAt > DateTime.UtcNow);
    }

    [Fact]
    public async Task Baskasinin_Rezervasyonu_Gorunmemeli()
    {
        var vehicle = await _factory.SeedVehicleAsync();
        var owner = await _factory.CreateDriverClientAsync();
        var other = await _factory.CreateDriverClientAsync();

        await owner.Client.PostAsync($"/api/v1/vehicles/{vehicle.Id}/reserve", null);

        var response = await other.Client.GetAsync(ActiveReservationUrl);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Iptal_Edilen_Ya_Da_Suruse_Donusen_Rezervasyon_Donmemeli()
    {
        var cancelled = await _factory.SeedVehicleAsync();
        var started = await _factory.SeedVehicleAsync();
        var driver = await _factory.CreateDriverClientAsync();

        await driver.Client.PostAsync($"/api/v1/vehicles/{cancelled.Id}/reserve", null);
        var cancel = await driver.Client.DeleteAsync($"/api/v1/vehicles/{cancelled.Id}/reservation");
        var afterCancel = await driver.Client.GetAsync(ActiveReservationUrl);

        await driver.Client.PostAsync($"/api/v1/vehicles/{started.Id}/reserve", null);
        var start = await driver.Client.PostAsJsonAsync("/api/rides/start", new { VehicleId = started.Id });
        var afterStart = await driver.Client.GetAsync(ActiveReservationUrl);

        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, afterCancel.StatusCode);
        Assert.Equal(HttpStatusCode.Created, start.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, afterStart.StatusCode);
    }

    [Fact]
    public async Task Suresi_Dolmus_Rezervasyon_Donmemeli()
    {
        // Arka plan işi süresi dolan rezervasyonu henüz serbest bırakmamış olabilir; uç yine de onu göstermemeli.
        var driver = await _factory.CreateDriverClientAsync();
        var now = DateTime.UtcNow;
        var vehicle = TestData.NewVehicle(registeredAt: now.AddHours(-1));
        vehicle.Reserve(driver.UserId, now - ReservationPolicy.Duration - TimeSpan.FromMinutes(1));

        await _factory.WithDbContextAsync(async dbContext =>
        {
            dbContext.Vehicles.Add(vehicle);
            await dbContext.SaveChangesAsync();
        });

        var response = await driver.Client.GetAsync(ActiveReservationUrl);

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Kimliksiz_Istek_401_Surucu_Olmayan_403_Almali()
    {
        var manager = await _factory.CreateUserClientAsync(ScootlyRoles.FleetManager);

        var anonymous = await _factory.CreateClient().GetAsync(ActiveReservationUrl);
        var byManager = await manager.Client.GetAsync(ActiveReservationUrl);

        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, byManager.StatusCode);
    }
}
