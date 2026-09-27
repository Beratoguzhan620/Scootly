using Scootly.Domain.Common;

namespace Scootly.Domain.Fleet;

public sealed class VehicleModel : ValueObject
{
    public const int BrandMaxLength = 100;
    public const int MaxRangeKm = 1000;

    public string Brand { get; }
    public int RangeKm { get; }

    public VehicleModel(string brand, int rangeKm)
    {
        if (string.IsNullOrWhiteSpace(brand))
            throw new DomainException("Marka boş olamaz.");

        if (brand.Length > BrandMaxLength)
            throw new DomainException($"Marka en fazla {BrandMaxLength} karakter olabilir.");

        if (rangeKm <= 0 || rangeKm > MaxRangeKm)
            throw new DomainException($"Menzil 1-{MaxRangeKm} km arasında olmalı.");

        Brand = brand.Trim();
        RangeKm = rangeKm;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Brand;
        yield return RangeKm;
    }
}
