using Scootly.Domain.Common;
using Scootly.Domain.Geo;
using Scootly.Domain.Riding;
using Xunit;

namespace Scootly.Domain.UnitTests;

public class RideFareTests
{
    private static Ride TamamlanmisSurus()
    {
        var ride = new Ride(RideId.New(), Guid.NewGuid(), Guid.NewGuid(), new GeoPoint(41.0, 29.0), DateTime.UtcNow.AddMinutes(-5));
        ride.Complete(new GeoPoint(41.01, 29.01), DateTime.UtcNow);
        return ride;
    }

    [Fact]
    public void Tamamlanmis_Surushe_Ucret_Yazilabilir()
    {
        var ride = TamamlanmisSurus();

        ride.ApplyFare(22.5m);

        Assert.Equal(22.5m, ride.Fare);
    }

    [Fact]
    public void Ucret_Ikinci_Kez_Yazilamaz()
    {
        // Ayni sure icin iki ucret yazmak alan modelinde bir HATA. Tekrar
        // gelen mesaji sessizce atlamak tuketicinin isi (ApplyRideFareCommandHandler).
        var ride = TamamlanmisSurus();
        ride.ApplyFare(22.5m);

        Assert.Throws<DomainException>(() => ride.ApplyFare(22.5m));
        Assert.Equal(22.5m, ride.Fare);
    }

    [Fact]
    public void Aktif_Surushe_Ucret_Yazilamaz()
    {
        var ride = new Ride(RideId.New(), Guid.NewGuid(), Guid.NewGuid(), new GeoPoint(41.0, 29.0), DateTime.UtcNow);

        Assert.Throws<DomainException>(() => ride.ApplyFare(10m));
    }

    [Fact]
    public void Negatif_Ucret_Yazilamaz()
    {
        Assert.Throws<DomainException>(() => TamamlanmisSurus().ApplyFare(-1m));
    }
}
