using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scootly.Api.Contracts.Responses;
using Scootly.Application.IntegrationEvents;
using Scootly.Application.Payments.Commands;
using Scootly.Domain.Fleet;
using Scootly.Domain.Riding;
using Scootly.Testing;
using Xunit;

namespace Scootly.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class RideFlowTests
{
    private readonly ScootlyApiFactory _factory;

    public RideFlowTests(ScootlyApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Kirala_Baslat_Bitir_Ode_Akisi_Uctan_Uca_Calismali()
    {
        var vehicle = await _factory.SeedVehicleAsync();
        var driver = await _factory.CreateDriverClientAsync();

        var reserve = await driver.Client.PostAsync($"/api/v1/vehicles/{vehicle.Id}/reserve", null);
        Assert.Equal(HttpStatusCode.OK, reserve.StatusCode);

        var start = await driver.Client.PostAsJsonAsync("/api/rides/start", new { VehicleId = vehicle.Id });
        Assert.Equal(HttpStatusCode.Created, start.StatusCode);
        var rideId = (await start.Content.ReadFromJsonAsync<StartRideResponse>())!.RideId;
        Assert.NotNull(start.Headers.Location);

        var active = await driver.Client.GetFromJsonAsync<RideResponse>("/api/rides/active");
        Assert.Equal(rideId, active!.Id);

        var inRide = await driver.Client.GetFromJsonAsync<VehicleResponseV2>($"/api/v1/vehicles/{vehicle.Id}");
        Assert.Equal("InRide", inRide!.Status);

        var complete = await driver.Client.PostAsJsonAsync($"/api/rides/{rideId}/complete", new { EndLatitude = 41.01, EndLongitude = 29.01 });
        Assert.Equal(HttpStatusCode.Accepted, complete.StatusCode);
        var completed = await complete.Content.ReadFromJsonAsync<CompleteRideResponse>();
        Assert.Equal("Pending", completed!.PaymentStatus);
        Assert.True(completed.Fare > 0);

        // Ödeme saga'sı: domain olayı aynı transaction'da outbox'a yazılmış olmalı.
        var outboxTypes = await _factory.WithDbContextAsync(db => db.OutboxMessages
            .Where(m => m.Payload.Contains(rideId.ToString()))
            .Select(m => m.EventType)
            .ToListAsync());
        Assert.Contains(IntegrationEventNames.RideCompleted, outboxTypes);

        // Tüketicinin yapacağı tahsilat adımı (mesajlaşma testte kapalı; sahte sağlayıcıyla doğrudan çalıştırılır).
        using (var scope = _factory.Services.CreateScope())
        {
            var charge = await scope.ServiceProvider.GetRequiredService<ChargeRideCommandHandler>().Handle(new ChargeRideCommand(rideId));
            Assert.Equal(ChargeOutcome.Approved, charge.Value);
        }

        var payment = await driver.Client.GetFromJsonAsync<PaymentStatusResponse>($"/api/rides/{rideId}/payment-status");
        Assert.Equal("Paid", payment!.PaymentStatus);

        var available = await driver.Client.GetFromJsonAsync<VehicleResponseV2>($"/api/v1/vehicles/{vehicle.Id}");
        Assert.Equal("Available", available!.Status);
    }

    [Fact]
    public async Task Baska_Kullanici_Surusu_Bitiremez_Ve_Goremez_IDOR()
    {
        var vehicle = await _factory.SeedVehicleAsync();
        var owner = await _factory.CreateDriverClientAsync();
        var attacker = await _factory.CreateDriverClientAsync();

        await owner.Client.PostAsync($"/api/v1/vehicles/{vehicle.Id}/reserve", null);
        var start = await owner.Client.PostAsJsonAsync("/api/rides/start", new { VehicleId = vehicle.Id });
        var rideId = (await start.Content.ReadFromJsonAsync<StartRideResponse>())!.RideId;

        var complete = await attacker.Client.PostAsJsonAsync($"/api/rides/{rideId}/complete", new { EndLatitude = 41.5, EndLongitude = 29.5 });
        var read = await attacker.Client.GetAsync($"/api/rides/{rideId}");
        var payment = await attacker.Client.GetAsync($"/api/rides/{rideId}/payment-status");

        Assert.Equal(HttpStatusCode.NotFound, complete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, payment.StatusCode);

        var ride = await _factory.WithDbContextAsync(db => db.Rides.AsNoTracking().SingleAsync(r => r.Id == rideId));
        Assert.Equal(RideStatus.Active, ride.Status);
    }

    [Fact]
    public async Task Baskasinin_Rezervasyonuyla_Surus_Baslatilamamali()
    {
        var vehicle = await _factory.SeedVehicleAsync();
        var reserver = await _factory.CreateDriverClientAsync();
        var other = await _factory.CreateDriverClientAsync();

        await reserver.Client.PostAsync($"/api/v1/vehicles/{vehicle.Id}/reserve", null);

        var start = await other.Client.PostAsJsonAsync("/api/rides/start", new { VehicleId = vehicle.Id });

        Assert.Equal(HttpStatusCode.Conflict, start.StatusCode);
        Assert.Equal("application/problem+json", start.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Surucu_Ayni_Anda_Iki_Arac_Rezerve_Edemez()
    {
        var first = await _factory.SeedVehicleAsync();
        var second = await _factory.SeedVehicleAsync();
        var driver = await _factory.CreateDriverClientAsync();

        var firstReserve = await driver.Client.PostAsync($"/api/v1/vehicles/{first.Id}/reserve", null);
        var secondReserve = await driver.Client.PostAsync($"/api/v1/vehicles/{second.Id}/reserve", null);

        Assert.Equal(HttpStatusCode.OK, firstReserve.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, secondReserve.StatusCode);
    }

    [Fact]
    public async Task Surucu_Kendi_Rezervasyonunu_Iptal_Edebilmeli_Baskasininkini_Edemez()
    {
        var vehicle = await _factory.SeedVehicleAsync();
        var owner = await _factory.CreateDriverClientAsync();
        var other = await _factory.CreateDriverClientAsync();

        await owner.Client.PostAsync($"/api/v1/vehicles/{vehicle.Id}/reserve", null);

        var byOther = await other.Client.DeleteAsync($"/api/v1/vehicles/{vehicle.Id}/reservation");
        var byOwner = await owner.Client.DeleteAsync($"/api/v1/vehicles/{vehicle.Id}/reservation");

        Assert.Equal(HttpStatusCode.NotFound, byOther.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, byOwner.StatusCode);

        var reloaded = await _factory.WithDbContextAsync(db => db.Vehicles.AsNoTracking().SingleAsync(v => v.Id == vehicle.Id));
        Assert.Equal(VehicleStatus.Available, reloaded.Status);
    }

    [Fact]
    public async Task Gecersiz_Bitis_Konumu_400_Donmeli()
    {
        var driver = await _factory.CreateDriverClientAsync();

        var response = await driver.Client.PostAsJsonAsync($"/api/rides/{Guid.NewGuid()}/complete", new { EndLatitude = 95.0, EndLongitude = 29.0 });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Kimliksiz_Istek_401_Donmeli()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/rides/start", new { VehicleId = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
