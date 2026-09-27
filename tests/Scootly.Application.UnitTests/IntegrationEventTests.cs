using Scootly.Application.IntegrationEvents;
using Scootly.Domain.Geo;
using Scootly.Domain.Riding;
using Scootly.Domain.Riding.Events;
using Xunit;

namespace Scootly.Application.UnitTests;

public sealed class IntegrationEventTests
{
    [Fact]
    public void Domain_Olayi_Entegrasyon_Olayina_Cevrilir()
    {
        var surucu = Guid.NewGuid();
        var arac = Guid.NewGuid();
        var baslangic = new DateTime(2026, 9, 27, 10, 0, 0, DateTimeKind.Utc);
        var ride = new Ride(RideId.New(), surucu, arac, new GeoPoint(41.0, 29.0), baslangic);
        ride.Complete(new GeoPoint(41.0, 29.01), baslangic.AddMinutes(12));

        var domainOlayi = ride.DomainEvents.OfType<RideCompletedEvent>().Single();
        var olay = domainOlayi.ToIntegrationEvent(ride);

        Assert.Equal(ride.Id, olay.RideId);
        Assert.Equal(surucu, olay.DriverId);
        Assert.Equal(arac, olay.VehicleId);
        Assert.Equal(12, olay.DurationMinutes, 2);
        Assert.True(olay.DistanceMeters > 0);
        Assert.NotEqual(Guid.Empty, olay.EventId);
        olay.Validate();
    }

    [Fact]
    public void Baska_Surushun_Olayi_Cevrilmez()
    {
        var a = new Ride(RideId.New(), Guid.NewGuid(), Guid.NewGuid(), new GeoPoint(41.0, 29.0), DateTime.UtcNow);
        var b = new Ride(RideId.New(), Guid.NewGuid(), Guid.NewGuid(), new GeoPoint(41.0, 29.0), DateTime.UtcNow);
        a.Complete(new GeoPoint(41.0, 29.01), DateTime.UtcNow);

        var olay = a.DomainEvents.OfType<RideCompletedEvent>().Single();

        Assert.Throws<ArgumentException>(() => olay.ToIntegrationEvent(b));
    }

    [Fact]
    public void Yonlendirme_Anahtarlari_Birbirinden_Farkli()
    {
        var adlar = new[]
        {
            RideCompletedIntegrationEvent.EventName,
            VehicleBatteryLowIntegrationEvent.EventName,
            PaymentAuthorizedIntegrationEvent.EventName
        };

        Assert.Equal(adlar.Length, adlar.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void Bos_Kimlikli_Olay_Gecersiz_Sayilir()
    {
        var olay = new RideCompletedIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), 5, 100);

        Assert.Throws<InvalidIntegrationEventException>(olay.Validate);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(101)]
    public void Batarya_Olayi_Aralik_Disinda_Gecersiz(int yuzde)
    {
        var olay = new VehicleBatteryLowIntegrationEvent(Guid.NewGuid(), DateTime.UtcNow, Guid.NewGuid(), yuzde);

        Assert.Throws<InvalidIntegrationEventException>(olay.Validate);
    }
}
