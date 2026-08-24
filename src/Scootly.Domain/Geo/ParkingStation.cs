namespace Scootly.Domain.Geo;

public sealed class ParkingStation
{
    public string Name { get; }
    public GeoPoint Location { get; }

    public ParkingStation(string name, GeoPoint location)
    {
        Name = name;
        Location = location;
    }
}