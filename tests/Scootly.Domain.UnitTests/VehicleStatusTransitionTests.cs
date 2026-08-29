using Scootly.Domain.Common;
using Scootly.Domain.Fleet;
using Scootly.Domain.Geo;
using Xunit;

namespace Scootly.Domain.UnitTests;

public class VehicleStatusTransitionTests
{
    private static Vehicle CreateAvailableVehicle()
    {
        return new Vehicle(
            VehicleId.New(),
            new VehicleModel("Xiaomi", 25),
            new GeoPoint(41.0, 29.0),
            new BatteryLevel(80));
    }

    [Fact]
    public void Musait_Aracta_StartRide_Cagrilirsa_Hata_Firlamali()
    {
        var vehicle = CreateAvailableVehicle();

        Assert.Throws<DomainException>(() => vehicle.StartRide());
    }

    [Fact]
    public void Rezerve_Araçta_StartRide_Cagrilirsa_Durum_InRide_Olmali()
    {
        var vehicle = CreateAvailableVehicle();
        vehicle.Reserve();

        vehicle.StartRide();

        Assert.Equal(VehicleStatus.InRide, vehicle.Status);
    }

    [Fact]
    public void Bakimdaki_Araç_Herhangi_Durumdan_Bakima_Alinabilmeli()
    {
        var vehicle = CreateAvailableVehicle();

        vehicle.SendToMaintenance();

        Assert.Equal(VehicleStatus.Maintenance, vehicle.Status);
    }
}