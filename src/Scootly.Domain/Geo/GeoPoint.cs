using Scootly.Domain.Common;

namespace Scootly.Domain.Geo;

public sealed class GeoPoint : ValueObject
{
    public double Latitude { get; }
    public double Longitude { get; }

    public GeoPoint(double latitude, double longitude)
    {
        if (latitude < -90 || latitude > 90)
            throw new DomainException("Enlem -90 ile 90 arasında olmalı.");

        if (longitude < -180 || longitude > 180)
            throw new DomainException("Boylam -180 ile 180 arasında olmalı.");

        Latitude = latitude;
        Longitude = longitude;
    }

    public double DistanceTo(GeoPoint other)
    {
        const double earthRadiusMeters = 6371000;

        var lat1Rad = DegreesToRadians(Latitude);
        var lat2Rad = DegreesToRadians(other.Latitude);
        var deltaLatRad = DegreesToRadians(other.Latitude - Latitude);
        var deltaLonRad = DegreesToRadians(other.Longitude - Longitude);

        var a = Math.Sin(deltaLatRad / 2) * Math.Sin(deltaLatRad / 2) +
                Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                Math.Sin(deltaLonRad / 2) * Math.Sin(deltaLonRad / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return earthRadiusMeters * c;
    }

    private static double DegreesToRadians(double degrees) => degrees * Math.PI / 180;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Latitude;
        yield return Longitude;
    }
}