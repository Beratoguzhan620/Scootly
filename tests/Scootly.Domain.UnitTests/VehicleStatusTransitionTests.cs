using Scootly.Domain.Common;
using Scootly.Domain.Fleet;
using Scootly.Domain.Fleet.Events;
using Scootly.Domain.Geo;
using Scootly.Domain.Riding;
using Xunit;

namespace Scootly.Domain.UnitTests;

public class VehicleStatusTransitionTests
{
    private static readonly DateTime Now = new(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);
    private static readonly Guid DriverA = Guid.NewGuid();
    private static readonly Guid DriverB = Guid.NewGuid();

    internal static Vehicle CreateAvailableVehicle(int battery = 80) => new(
        VehicleId.New(),
        new VehicleModel("Xiaomi", 25),
        new GeoPoint(41.0, 29.0),
        new BatteryLevel(battery),
        Now);

    [Fact]
    public void Musait_Aracta_StartRide_Cagrilirsa_Hata_Firlamali()
    {
        var vehicle = CreateAvailableVehicle();

        Assert.Throws<DomainException>(() => vehicle.StartRide(DriverA, Now));
    }

    [Fact]
    public void Rezerve_Aracta_Rezerve_Eden_Surucu_StartRide_Cagirirsa_Durum_InRide_Olmali()
    {
        var vehicle = CreateAvailableVehicle();
        vehicle.Reserve(DriverA, Now);

        vehicle.StartRide(DriverA, Now);

        Assert.Equal(VehicleStatus.InRide, vehicle.Status);
        Assert.Null(vehicle.ReservedBy);
        Assert.Null(vehicle.ReservedAt);
    }

    [Fact]
    public void Baskasinin_Rezervasyonuyla_Surus_Baslatilamamali()
    {
        var vehicle = CreateAvailableVehicle();
        vehicle.Reserve(DriverA, Now);

        var ex = Assert.Throws<DomainException>(() => vehicle.StartRide(DriverB, Now));

        Assert.Contains("başka bir sürücü", ex.Message);
        Assert.Equal(VehicleStatus.Reserved, vehicle.Status);
    }

    [Fact]
    public void Rezervasyon_Sahibini_Ve_Zamanini_Kaydetmeli()
    {
        var vehicle = CreateAvailableVehicle();

        vehicle.Reserve(DriverA, Now);

        Assert.Equal(DriverA, vehicle.ReservedBy);
        Assert.Equal(Now, vehicle.ReservedAt);
    }

    [Fact]
    public void Bos_Surucu_Kimligiyle_Rezervasyon_Yapilamamali()
    {
        var vehicle = CreateAvailableVehicle();

        Assert.Throws<DomainException>(() => vehicle.Reserve(Guid.Empty, Now));
    }

    [Fact]
    public void Rezervasyonu_Yalnizca_Sahibi_Iptal_Edebilmeli()
    {
        var vehicle = CreateAvailableVehicle();
        vehicle.Reserve(DriverA, Now);

        Assert.Throws<DomainException>(() => vehicle.CancelReservation(DriverB, Now));

        vehicle.CancelReservation(DriverA, Now);
        Assert.Equal(VehicleStatus.Available, vehicle.Status);
        Assert.Null(vehicle.ReservedBy);
    }

    [Fact]
    public void Suresi_Dolmamis_Rezervasyon_Sistem_Tarafindan_Kaldirilamamali()
    {
        var vehicle = CreateAvailableVehicle();
        vehicle.Reserve(DriverA, Now);

        Assert.Throws<DomainException>(() => vehicle.ExpireReservation(Now.AddMinutes(5)));
    }

    [Fact]
    public void Suresi_Dolan_Rezervasyon_Sistem_Tarafindan_Kaldirilabilmeli()
    {
        var vehicle = CreateAvailableVehicle();
        vehicle.Reserve(DriverA, Now);

        vehicle.ExpireReservation(Now + ReservationPolicy.Duration);

        Assert.Equal(VehicleStatus.Available, vehicle.Status);
        Assert.Null(vehicle.ReservedBy);
    }

    [Fact]
    public void Surus_Tamamlaninca_Arac_Park_Edildigi_Konumda_Musait_Olmali()
    {
        var vehicle = CreateAvailableVehicle();
        vehicle.Reserve(DriverA, Now);
        vehicle.StartRide(DriverA, Now);

        var parkedAt = new GeoPoint(41.02, 29.02);
        vehicle.CompleteRide(parkedAt, Now.AddMinutes(10));

        Assert.Equal(VehicleStatus.Available, vehicle.Status);
        Assert.Equal(parkedAt, vehicle.Location);
    }

    [Fact]
    public void Terk_Edilen_Surusten_Sonra_Arac_Bakima_Alinmali()
    {
        var vehicle = CreateAvailableVehicle();
        vehicle.Reserve(DriverA, Now);
        vehicle.StartRide(DriverA, Now);

        vehicle.EndAbandonedRide(Now.AddHours(3));

        Assert.Equal(VehicleStatus.Maintenance, vehicle.Status);
    }

    [Fact]
    public void Musait_Arac_Bakima_Alinabilmeli_Ve_Hizmete_Donebilmeli()
    {
        var vehicle = CreateAvailableVehicle();

        vehicle.SendToMaintenance(Now);
        Assert.Equal(VehicleStatus.Maintenance, vehicle.Status);

        vehicle.ReturnToService(Now);
        Assert.Equal(VehicleStatus.Available, vehicle.Status);
    }

    [Fact]
    public void Rezerve_Arac_Bakima_Alinirsa_Rezervasyon_Temizlenmeli()
    {
        var vehicle = CreateAvailableVehicle();
        vehicle.Reserve(DriverA, Now);

        vehicle.SendToMaintenance(Now);

        Assert.Equal(VehicleStatus.Maintenance, vehicle.Status);
        Assert.Null(vehicle.ReservedBy);
    }

    [Fact]
    public void Suruşteki_Arac_Bakima_Alinamamali()
    {
        var vehicle = CreateAvailableVehicle();
        vehicle.Reserve(DriverA, Now);
        vehicle.StartRide(DriverA, Now);

        Assert.Throws<DomainException>(() => vehicle.SendToMaintenance(Now));
        Assert.Equal(VehicleStatus.InRide, vehicle.Status);
    }

    [Fact]
    public void Bakimda_Olmayan_Arac_Hizmete_Dondurulemez()
    {
        var vehicle = CreateAvailableVehicle();

        Assert.Throws<DomainException>(() => vehicle.ReturnToService(Now));
    }

    [Fact]
    public void Durum_Degisikligi_Olayi_Verilen_Zamanla_Uretilmeli()
    {
        var vehicle = CreateAvailableVehicle();
        vehicle.ClearDomainEvents();

        vehicle.Reserve(DriverA, Now);

        var statusChanged = Assert.Single(vehicle.DomainEvents.OfType<VehicleStatusChangedEvent>());
        Assert.Equal(VehicleStatus.Available, statusChanged.OldStatus);
        Assert.Equal(VehicleStatus.Reserved, statusChanged.NewStatus);
        Assert.Equal(Now, statusChanged.OccurredOn);
    }

    [Fact]
    public void Musait_Aracta_UpdateModel_Modeli_Guncellemeli()
    {
        var vehicle = CreateAvailableVehicle();
        var newModel = new VehicleModel("Segway", 30);

        vehicle.UpdateModel(newModel);

        Assert.Equal(newModel, vehicle.Model);
    }

    [Fact]
    public void Rezerve_Aracta_UpdateModel_Modeli_Guncelleyebilmeli()
    {
        var vehicle = CreateAvailableVehicle();
        vehicle.Reserve(DriverA, Now);
        var newModel = new VehicleModel("Segway", 30);

        vehicle.UpdateModel(newModel);

        Assert.Equal(newModel, vehicle.Model);
        Assert.Equal(VehicleStatus.Reserved, vehicle.Status);
    }

    [Fact]
    public void Suruşteki_Arac_UpdateModel_Ile_Duzenlenemez()
    {
        var vehicle = CreateAvailableVehicle();
        vehicle.Reserve(DriverA, Now);
        vehicle.StartRide(DriverA, Now);
        var newModel = new VehicleModel("Segway", 30);

        var ex = Assert.Throws<DomainException>(() => vehicle.UpdateModel(newModel));

        Assert.Contains("düzenlenemez", ex.Message);
    }
}
