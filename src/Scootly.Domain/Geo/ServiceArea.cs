namespace Scootly.Domain.Geo;

public sealed class ServiceArea
{
    public string Name { get; }
    public IReadOnlyList<GeoPoint> Boundary { get; }

    public ServiceArea(string name, IReadOnlyList<GeoPoint> boundary)
    {
        Name = name;
        Boundary = boundary;
    }
}