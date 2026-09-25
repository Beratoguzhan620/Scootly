using Scootly.Domain.Common;

namespace Scootly.Domain.Telemetry;

public sealed class DeviceId : ValueObject
{
    public Guid Value { get; }

    public DeviceId(Guid value)
    {
        Value = value;
    }

    public static DeviceId New() => new(Guid.NewGuid());

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }
}