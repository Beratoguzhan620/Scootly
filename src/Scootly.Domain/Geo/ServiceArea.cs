namespace Scootly.Domain.Geo;

public sealed class ServiceArea
{
    public string Name { get; private set; }
    public IReadOnlyList<GeoPoint> Boundary { get; private set; }

    private ServiceArea()
    {
        Name = null!;
        Boundary = new List<GeoPoint>();
    }

    public ServiceArea(string name, IReadOnlyList<GeoPoint> boundary)
    {
        Name = name;
        Boundary = boundary;
    }
}