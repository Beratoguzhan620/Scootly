namespace Scootly.Domain.Fleet;

public sealed class VehicleModel
{
    public string Brand { get; }
    public int RangeKm { get; }

    public VehicleModel(string brand, int rangeKm)
    {
        Brand = brand;
        RangeKm = rangeKm;
    }
}