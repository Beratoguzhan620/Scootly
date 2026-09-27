using Scootly.Domain.Common;

namespace Scootly.Domain.Fleet;

public sealed class BatteryLevel : ValueObject
{
    /// <summary>Bu yüzdenin altındaki batarya "düşük" sayılır ve saha ekibine görev düşer.</summary>
    public const int LowThresholdPercentage = 20;

    public int Percentage { get; }

    public BatteryLevel(int percentage)
    {
        if (percentage < 0 || percentage > 100)
            throw new DomainException("Batarya yüzdesi 0-100 arasında olmalı.");

        Percentage = percentage;
    }

    public bool IsLow => Percentage < LowThresholdPercentage;

    /// <summary>Batarya bu okumayla düşük eşiğin altına ilk kez indiyse true döner.</summary>
    public bool HasDroppedBelowLowThresholdFrom(BatteryLevel previous) => IsLow && !previous.IsLow;

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Percentage;
    }
}
