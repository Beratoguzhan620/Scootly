using Scootly.Domain.Common;

namespace Scootly.Domain.Geo;

public sealed class ServiceArea
{
    public const int NameMaxLength = 200;
    public const int MaxBoundaryPoints = 500;

    private readonly List<GeoPoint> _boundary = new();

    public string Name { get; private set; }
    public IReadOnlyList<GeoPoint> Boundary => _boundary.AsReadOnly();

    private ServiceArea()
    {
        Name = null!;
    }

    public ServiceArea(string name, IReadOnlyList<GeoPoint> boundary)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new DomainException("Hizmet bölgesi adı boş olamaz.");

        if (name.Length > NameMaxLength)
            throw new DomainException($"Hizmet bölgesi adı en fazla {NameMaxLength} karakter olabilir.");

        if (boundary.Count < 3 || boundary.Count > MaxBoundaryPoints)
            throw new DomainException($"Hizmet bölgesi sınırı 3-{MaxBoundaryPoints} noktadan oluşmalı.");

        if (boundary.Distinct().Count() != boundary.Count)
            throw new DomainException("Hizmet bölgesi sınır noktaları birbirinden farklı olmalı (poligonu kapatmak için ilk noktayı tekrar etmeyin).");

        Name = name.Trim();
        _boundary.AddRange(boundary);
    }
}
