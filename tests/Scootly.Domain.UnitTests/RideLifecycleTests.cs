using Scootly.Domain.Common;
using Scootly.Domain.Geo;
using Scootly.Domain.Riding;
using Scootly.Domain.Riding.Events;
using Xunit;

namespace Scootly.Domain.UnitTests;

public class RideLifecycleTests
{
    private static readonly DateTime StartedAt = new(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);

    private static Ride CreateActiveRide() => new(
        RideId.New(),
        Guid.NewGuid(),
        Guid.NewGuid(),
        new GeoPoint(41.0, 29.0),
        StartedAt);

    [Fact]
    public void Ride_Complete_Cagrilinca_Durum_Completed_Ve_Odeme_Bekliyor_Olmali()
    {
        var ride = CreateActiveRide();

        ride.Complete(new GeoPoint(41.01, 29.01), StartedAt.AddMinutes(10), Tariff.Standard);

        Assert.Equal(RideStatus.Completed, ride.Status);
        Assert.Equal(PaymentStatus.Pending, ride.PaymentStatus);
        Assert.Equal(25.00m, ride.Fare);
    }

    [Fact]
    public void Ride_Complete_Sonrasi_Mesafe_Sifirdan_Buyuk_Olmali()
    {
        var ride = CreateActiveRide();

        ride.Complete(new GeoPoint(41.01, 29.01), StartedAt.AddMinutes(10), Tariff.Standard);

        var completed = Assert.Single(ride.DomainEvents.OfType<RideCompletedEvent>());
        Assert.True(completed.DistanceMeters > 0);
        Assert.Equal(ride.DriverId, completed.DriverId);
        Assert.Equal(ride.VehicleId, completed.VehicleId);
    }

    [Fact]
    public void Bitis_Zamani_Baslangictan_Once_Olamaz()
    {
        var ride = CreateActiveRide();

        Assert.Throws<DomainException>(() => ride.Complete(new GeoPoint(41.01, 29.01), StartedAt.AddMinutes(-1), Tariff.Standard));
    }

    [Fact]
    public void Tamamlanmis_Surus_Tekrar_Tamamlanamaz()
    {
        var ride = CreateActiveRide();
        ride.Complete(new GeoPoint(41.01, 29.01), StartedAt.AddMinutes(10), Tariff.Standard);

        Assert.Throws<DomainException>(() => ride.Complete(new GeoPoint(41.01, 29.01), StartedAt.AddMinutes(20), Tariff.Standard));
    }

    [Fact]
    public void Terk_Edilen_Surus_Gecen_Sure_Kadar_Ucretlendirilmeli()
    {
        var ride = CreateActiveRide();

        ride.Abandon(new GeoPoint(41.02, 29.02), StartedAt.AddHours(2), Tariff.Standard);

        Assert.Equal(RideStatus.Abandoned, ride.Status);
        Assert.Equal(PaymentStatus.Pending, ride.PaymentStatus);
        Assert.Equal(300.00m, ride.Fare);
        Assert.Single(ride.DomainEvents.OfType<RideAbandonedEvent>());
    }

    [Fact]
    public void Surucu_Ve_Arac_Kimligi_Bos_Olamaz()
    {
        Assert.Throws<DomainException>(() => new Ride(RideId.New(), Guid.Empty, Guid.NewGuid(), new GeoPoint(41, 29), StartedAt));
        Assert.Throws<DomainException>(() => new Ride(RideId.New(), Guid.NewGuid(), Guid.Empty, new GeoPoint(41, 29), StartedAt));
    }
}
