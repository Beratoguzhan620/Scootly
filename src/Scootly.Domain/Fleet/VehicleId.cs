using Scootly.Domain.Common;

namespace Scootly.Domain.Fleet;

public sealed class VehicleId : ValueObject
{
    public Guid Value { get; }

    public VehicleId(Guid value)
    {
        Value = value;
    }

    public static VehicleId New() => new(Guid.NewGuid());

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}