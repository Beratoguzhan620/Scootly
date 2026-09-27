using Scootly.Domain.Fleet;
using Scootly.Domain.Fleet.Events;
using Scootly.Domain.Geo;
using Xunit;

namespace Scootly.Domain.UnitTests;

public class VehicleTelemetryTests
{
    private static readonly DateTime Now = new(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Telemetri_Konum_Ve_Bataryayi_Guncellemeli()
    {
        var vehicle = VehicleStatusTransitionTests.CreateAvailableVehicle();

        var applied = vehicle.ReportTelemetry(new GeoPoint(41.05, 29.05), new BatteryLevel(70), Now);

        Assert.True(applied);
        Assert.Equal(new GeoPoint(41.05, 29.05), vehicle.Location);
        Assert.Equal(70, vehicle.Battery.Percentage);
        Assert.Equal(Now, vehicle.LastTelemetryAt);
    }

    [Fact]
    public void Daha_Eski_Okuma_Yok_Sayilmali()
    {
        var vehicle = VehicleStatusTransitionTests.CreateAvailableVehicle();
        vehicle.ReportTelemetry(new GeoPoint(41.05, 29.05), new BatteryLevel(70), Now);

        var applied = vehicle.ReportTelemetry(new GeoPoint(40.0, 28.0), new BatteryLevel(90), Now.AddSeconds(-10));

        Assert.False(applied);
        Assert.Equal(70, vehicle.Battery.Percentage);
    }

    [Fact]
    public void Batarya_Esigin_Altina_Ilk_Kez_Inince_Olay_Uretilmeli()
    {
        var vehicle = VehicleStatusTransitionTests.CreateAvailableVehicle(battery: 25);
        vehicle.ClearDomainEvents();

        vehicle.ReportTelemetry(vehicle.Location, new BatteryLevel(19), Now);

        var batteryLow = Assert.Single(vehicle.DomainEvents.OfType<VehicleBatteryLowEvent>());
        Assert.Equal(19, batteryLow.BatteryPercentage);
    }

    [Fact]
    public void Zaten_Dusuk_Olan_Batarya_Icin_Tekrar_Olay_Uretilmemeli()
    {
        var vehicle = VehicleStatusTransitionTests.CreateAvailableVehicle(battery: 15);
        vehicle.ClearDomainEvents();

        vehicle.ReportTelemetry(vehicle.Location, new BatteryLevel(14), Now);

        Assert.Empty(vehicle.DomainEvents.OfType<VehicleBatteryLowEvent>());
    }
}
