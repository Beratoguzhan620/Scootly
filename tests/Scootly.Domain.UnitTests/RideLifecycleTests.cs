using Scootly.Domain.Geo;
using Scootly.Domain.Riding;
using Xunit;

namespace Scootly.Domain.UnitTests;

public class RideLifecycleTests
{
    [Fact]
    public void Ride_Complete_Cagrilinca_Durum_Completed_Olmali()
    {
        var ride = new Ride(
            RideId.New(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new GeoPoint(41.0, 29.0),
            DateTime.UtcNow);

        ride.Complete(new GeoPoint(41.01, 29.01), DateTime.UtcNow.AddMinutes(10));

        Assert.Equal(RideStatus.Completed, ride.Status);
    }

    [Fact]
    public void Ride_Complete_Sonrasi_Mesafe_Sifirdan_Buyuk_Olmali()
    {
        var ride = new Ride(
            RideId.New(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            new GeoPoint(41.0, 29.0),
            DateTime.UtcNow);

        ride.Complete(new GeoPoint(41.01, 29.01), DateTime.UtcNow.AddMinutes(10));

        Assert.True(ride.EndLocation!.DistanceTo(ride.StartLocation) > 0);
    }
}