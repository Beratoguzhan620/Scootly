namespace Scootly.Domain.Geo;

public sealed class NoParkingZone
{
    public string Name { get; }
    public IReadOnlyList<GeoPoint> Boundary { get; }

    public NoParkingZone(string name, IReadOnlyList<GeoPoint> boundary)
    {
        Name = name;
        Boundary = boundary;
    }
}