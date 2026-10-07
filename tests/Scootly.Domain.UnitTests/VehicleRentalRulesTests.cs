using Scootly.Domain.Common;
using Scootly.Domain.Fleet;
using Scootly.Domain.Fleet.Events;
using Scootly.Domain.Geo;
using Xunit;

namespace Scootly.Domain.UnitTests;

/// <summary>Batarya kiralama eşiği, kayıp durumu ve sürüş bitişinde konumun yetkili kaynağı.</summary>
public class VehicleRentalRulesTests
{
    private static readonly DateTime Now = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Driver = Guid.NewGuid();

    [Theory]
    [InlineData(BatteryLevel.MinimumRentablePercentage - 1, false)]
    [InlineData(BatteryLevel.MinimumRentablePercentage, true)]
    [InlineData(100, true)]
    public void Kiralanabilirlik_Esigi_Dogru_Olmali(int percentage, bool expected)
    {
        Assert.Equal(expected, new BatteryLevel(percentage).IsRentable);
    }

    [Fact]
    public void Bataryasi_Esigin_Altindaki_Arac_Rezerve_Edilemez()
    {
        var vehicle = VehicleStatusTransitionTests.CreateAvailableVehicle(battery: BatteryLevel.MinimumRentablePercentage - 1);

        var ex = Assert.Throws<DomainException>(() => vehicle.Reserve(Driver, Now));

        Assert.Contains("batarya", ex.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(VehicleStatus.Available, vehicle.Status);
    }

    [Fact]
    public void Bataryasi_Esikte_Olan_Arac_Rezerve_Edilebilir()
    {
        var vehicle = VehicleStatusTransitionTests.CreateAvailableVehicle(battery: BatteryLevel.MinimumRentablePercentage);

        vehicle.Reserve(Driver, Now);

        Assert.Equal(VehicleStatus.Reserved, vehicle.Status);
    }

    [Fact]
    public void Rezerve_Arac_Kayip_Isaretlenince_Rezervasyon_Temizlenmeli()
    {
        var vehicle = VehicleStatusTransitionTests.CreateAvailableVehicle();
        vehicle.Reserve(Driver, Now);

        vehicle.MarkLost(Now);

        Assert.Equal(VehicleStatus.Lost, vehicle.Status);
        Assert.Null(vehicle.ReservedBy);
        Assert.Null(vehicle.ReservedAt);
        Assert.Contains(vehicle.DomainEvents.OfType<VehicleStatusChangedEvent>(), e => e.NewStatus == VehicleStatus.Lost);
    }

    [Fact]
    public void Bakimdaki_Arac_Kayip_Isaretlenip_Bulununca_Hizmete_Donebilmeli()
    {
        var vehicle = VehicleStatusTransitionTests.CreateAvailableVehicle();
        vehicle.SendToMaintenance(Now);

        vehicle.MarkLost(Now);
        vehicle.ReturnToService(Now);

        Assert.Equal(VehicleStatus.Available, vehicle.Status);
    }

    [Fact]
    public void Suruşteki_Arac_Kayip_Isaretlenemez()
    {
        var vehicle = VehicleStatusTransitionTests.CreateAvailableVehicle();
        vehicle.Reserve(Driver, Now);
        vehicle.StartRide(Driver, Now);

        Assert.Throws<DomainException>(() => vehicle.MarkLost(Now));
        Assert.Equal(VehicleStatus.InRide, vehicle.Status);
    }

    [Fact]
    public void Kayip_Arac_Tekrar_Kayip_Isaretlenemez()
    {
        var vehicle = VehicleStatusTransitionTests.CreateAvailableVehicle();
        vehicle.MarkLost(Now);

        Assert.Throws<DomainException>(() => vehicle.MarkLost(Now));
    }

    [Fact]
    public void Taze_Telemetri_Varsa_Surus_Bitisinde_Aracin_Bildirdigi_Konum_Kullanilmali()
    {
        var vehicle = InRideVehicle();
        var reportedByDevice = new GeoPoint(41.05, 29.05);
        vehicle.ReportTelemetry(reportedByDevice, new BatteryLevel(70), Now.AddSeconds(-30));

        var parkedAt = vehicle.CompleteRide(new GeoPoint(40.0, 28.0), Now);

        Assert.Equal(reportedByDevice, parkedAt);
        Assert.Equal(reportedByDevice, vehicle.Location);
        Assert.Equal(VehicleStatus.Available, vehicle.Status);
    }

    [Fact]
    public void Telemetri_Eskiyse_Surus_Bitisinde_Istemcinin_Bildirdigi_Konum_Kullanilmali()
    {
        var vehicle = InRideVehicle();
        vehicle.ReportTelemetry(new GeoPoint(41.05, 29.05), new BatteryLevel(70), Now - Vehicle.TelemetryFreshness - TimeSpan.FromSeconds(1));
        var reportedByClient = new GeoPoint(41.1, 29.1);

        var parkedAt = vehicle.CompleteRide(reportedByClient, Now);

        Assert.Equal(reportedByClient, parkedAt);
        Assert.Equal(reportedByClient, vehicle.Location);
    }

    [Fact]
    public void Hic_Telemetri_Yoksa_Surus_Bitisinde_Istemcinin_Bildirdigi_Konum_Kullanilmali()
    {
        var vehicle = InRideVehicle();
        var reportedByClient = new GeoPoint(41.1, 29.1);

        var parkedAt = vehicle.CompleteRide(reportedByClient, Now);

        Assert.Equal(reportedByClient, parkedAt);
        Assert.False(vehicle.HasFreshTelemetry(Now));
    }

    private static Vehicle InRideVehicle()
    {
        var vehicle = VehicleStatusTransitionTests.CreateAvailableVehicle();
        vehicle.Reserve(Driver, Now.AddMinutes(-20));
        vehicle.StartRide(Driver, Now.AddMinutes(-15));
        return vehicle;
    }
}
