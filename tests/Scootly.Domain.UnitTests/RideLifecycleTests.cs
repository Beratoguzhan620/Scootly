using System;
using System.Collections.Generic;
using System.Text;

using Scootly.Domain.Common;
using Scootly.Domain.Fleet;
using Scootly.Domain.Geo;
using Scootly.Domain.Riding;
using Scootly.Domain.Riding.Events;
using Xunit;

namespace Scootly.Domain.UnitTests;

public class RideLifecycleTests
{
    private static readonly DateTime Baslangic = new(2026, 8, 26, 10, 0, 0, DateTimeKind.Utc);

    private static Ride SurusOlustur()
    {
        return new Ride(
            RideId.New(),
            Guid.NewGuid(),
            VehicleId.New(),
            new GeoPoint(37.00, 35.32),
            Baslangic);
    }

    [Fact]
    public void Yeni_Surus_Active_Durumunda_Baslamali()
    {
        var surus = SurusOlustur();

        Assert.Equal(RideStatus.Active, surus.Status);
    }

    [Fact]
    public void Yeni_Surus_RideStartedEvent_Uretmeli()
    {
        var surus = SurusOlustur();

        var olay = Assert.Single(surus.DomainEvents);
        Assert.IsType<RideStartedEvent>(olay);
    }

    [Fact]
    public void Complete_Durumu_Completed_Yapmali()
    {
        var surus = SurusOlustur();

        surus.Complete(new GeoPoint(37.01, 35.33), Baslangic.AddMinutes(15));

        Assert.Equal(RideStatus.Completed, surus.Status);
    }

    [Fact]
    public void Complete_Mesafeyi_Sifirdan_Buyuk_Hesaplamali()
    {
        var surus = SurusOlustur();

        surus.Complete(new GeoPoint(37.01, 35.33), Baslangic.AddMinutes(15));

        var olay = Assert.IsType<RideCompletedEvent>(surus.DomainEvents.Last());
        Assert.True(olay.DistanceMeters > 0);
    }

    [Fact]
    public void Complete_Sureyi_Dogru_Hesaplamali()
    {
        var surus = SurusOlustur();

        surus.Complete(new GeoPoint(37.01, 35.33), Baslangic.AddMinutes(15));

        var olay = Assert.IsType<RideCompletedEvent>(surus.DomainEvents.Last());
        Assert.Equal(TimeSpan.FromMinutes(15), olay.Duration);
    }

    [Fact]
    public void Complete_Iki_Kez_Cagrilirsa_DomainException_Firlatmali()
    {
        var surus = SurusOlustur();
        surus.Complete(new GeoPoint(37.01, 35.33), Baslangic.AddMinutes(15));

        Assert.Throws<DomainException>(() =>
            surus.Complete(new GeoPoint(37.02, 35.34), Baslangic.AddMinutes(20)));
    }

    [Fact]
    public void Complete_Gecmis_Bitis_Zamaniyla_DomainException_Firlatmali()
    {
        var surus = SurusOlustur();

        Assert.Throws<DomainException>(() =>
            surus.Complete(new GeoPoint(37.01, 35.33), Baslangic.AddMinutes(-5)));
    }
}
