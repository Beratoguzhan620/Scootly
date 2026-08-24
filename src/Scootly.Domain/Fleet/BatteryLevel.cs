using Scootly.Domain.Common;

namespace Scootly.Domain.Fleet;

public sealed class BatteryLevel : ValueObject
{
    public int Percentage { get; }

    public BatteryLevel(int percentage)
    {
        if (percentage < 0 || percentage > 100)
            throw new DomainException("Batarya yüzdesi 0-100 arasında olmalı.");

        Percentage = percentage;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Percentage;
    }
}