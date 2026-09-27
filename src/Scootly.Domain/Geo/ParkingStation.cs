using Scootly.Domain.Common;

namespace Scootly.Domain.Geo;

public sealed class ParkingStation
{
    public string Name { get; }
    public GeoPoint Location { get; }

    public ParkingStation(string name, GeoPoint location)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Park istasyonu adı boş olamaz.");

        Name = name;
        Location = location;
    }
}
