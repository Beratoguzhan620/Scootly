using Scootly.Domain.Common;

namespace Scootly.Domain.Riding;

public sealed class RideId : ValueObject
{
    public Guid Value { get; }

    public RideId(Guid value)
    {
        Value = value;
    }

    public static RideId New() => new(Guid.NewGuid());

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}