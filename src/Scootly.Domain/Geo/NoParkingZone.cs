using Scootly.Domain.Common;

namespace Scootly.Domain.Geo;

public sealed class NoParkingZone
{
    public string Name { get; }
    public IReadOnlyList<GeoPoint> Boundary { get; }

    public NoParkingZone(string name, IReadOnlyList<GeoPoint> boundary)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Park yasağı bölgesi adı boş olamaz.");

        if (boundary.Count < 3)
            throw new DomainException("Park yasağı bölgesi sınırı en az 3 noktadan oluşmalı.");

        Name = name;
        Boundary = boundary.ToList();
    }
}
