using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Scootly.Api.Contracts.Responses;
using Scootly.Application.IntegrationEvents;
using Scootly.Testing;
using Xunit;

namespace Scootly.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class DeviceAndTelemetryTests
{
    private readonly ScootlyApiFactory _factory;

    public DeviceAndTelemetryTests(ScootlyApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Yanlis_Cihaz_Sirri_Reddedilmeli()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/device-auth/token", new
        {
            ClientId = TestSecrets.DeviceClientId,
            ClientSecret = "yanlis-sir"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Cihaz_Tokeni_Kullanici_Uclarinda_Reddedilmeli_500_Olmamali()
    {
        var device = await _factory.CreateDeviceClientAsync();
        var vehicle = await _factory.SeedVehicleAsync();

        var reserve = await device.PostAsync($"/api/v1/vehicles/{vehicle.Id}/reserve", null);
        var start = await device.PostAsJsonAsync("/api/rides/start", new { VehicleId = vehicle.Id });

        Assert.Equal(HttpStatusCode.Forbidden, reserve.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, start.StatusCode);
    }

    [Fact]
    public async Task Kullanici_Tokeni_Telemetri_Gonderemez()
    {
        var driver = await _factory.CreateDriverClientAsync();
        var vehicle = await _factory.SeedVehicleAsync();

        var response = await driver.Client.PostAsJsonAsync("/api/telemetry/batch", new
        {
            Readings = new[] { new { VehicleId = vehicle.Id, Latitude = 41.0, Longitude = 29.0, BatteryPercentage = 10 } }
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Tokensiz_Telemetri_401_Donmeli()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/telemetry/batch", new { Readings = Array.Empty<object>() });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Cok_Buyuk_Parti_Reddedilmeli()
    {
        var device = await _factory.CreateDeviceClientAsync();
        var readings = Enumerable.Range(0, 501)
            .Select(_ => new { VehicleId = Guid.NewGuid(), Latitude = 41.0, Longitude = 29.0, BatteryPercentage = 50 });

        var response = await device.PostAsJsonAsync("/api/telemetry/batch", new { Readings = readings });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Gecersiz_Okuma_Tum_Partiyi_Reddetmeli()
    {
        var device = await _factory.CreateDeviceClientAsync();
        var vehicle = await _factory.SeedVehicleAsync();

        var response = await device.PostAsJsonAsync("/api/telemetry/batch", new
        {
            Readings = new object[]
            {
                new { VehicleId = vehicle.Id, Latitude = 41.0, Longitude = 29.0, BatteryPercentage = 50 },
                new { VehicleId = vehicle.Id, Latitude = 41.0, Longitude = 29.0, BatteryPercentage = 150 }
            }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Kayitsiz_Arac_Okumalari_Reddedilmeli_Kayitlilar_Islenmeli()
    {
        var device = await _factory.CreateDeviceClientAsync();
        var vehicle = await _factory.SeedVehicleAsync(batteryPercentage: 25);
        var unknownVehicleId = Guid.NewGuid();

        var response = await device.PostAsJsonAsync("/api/telemetry/batch", new
        {
            Readings = new object[]
            {
                new { VehicleId = vehicle.Id, Latitude = 41.05, Longitude = 29.05, BatteryPercentage = 15, RecordedAt = DateTime.UtcNow },
                new { VehicleId = unknownVehicleId, Latitude = 41.0, Longitude = 29.0, BatteryPercentage = 90 }
            }
        });

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<TelemetryBatchResponse>();
        Assert.Equal(1, body!.Accepted);
        Assert.Equal([unknownVehicleId], body.RejectedVehicleIds);

        // Arka plan tüketicisi aracın son bilinen durumunu günceller ve eşik aşımı olayını outbox'a yazar.
        var updated = await Eventually.WaitAsync(
            () => _factory.WithDbContextAsync(db => db.Vehicles.AsNoTracking().SingleAsync(v => v.Id == vehicle.Id)),
            v => v.Battery.Percentage == 15);

        Assert.Equal(15, updated.Battery.Percentage);
        Assert.Equal(41.05, updated.Location.Latitude, precision: 5);

        var batteryLowEvents = await _factory.WithDbContextAsync(db => db.OutboxMessages
            .CountAsync(m => m.EventType == IntegrationEventNames.VehicleBatteryLow && m.Payload.Contains(vehicle.Id.ToString())));
        Assert.Equal(1, batteryLowEvents);

        var readings = await _factory.WithDbContextAsync(db => db.TelemetryReadings.CountAsync(t => t.VehicleId == vehicle.Id));
        Assert.Equal(1, readings);
    }
}
