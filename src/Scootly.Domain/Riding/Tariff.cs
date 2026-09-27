using Scootly.Domain.Common;

namespace Scootly.Domain.Riding;

/// <summary>Sürüş ücreti: açılış bedeli + başlamış her dakika için dakika ücreti (en az 1 dakika).</summary>
public sealed class Tariff : ValueObject
{
    public static readonly Tariff Standard = new(unlockFee: 0m, perMinuteRate: 2.5m);

    public decimal UnlockFee { get; }
    public decimal PerMinuteRate { get; }

    public Tariff(decimal unlockFee, decimal perMinuteRate)
    {
        if (unlockFee < 0)
            throw new DomainException("Açılış ücreti negatif olamaz.");

        if (perMinuteRate <= 0)
            throw new DomainException("Dakika ücreti sıfırdan büyük olmalı.");

        UnlockFee = unlockFee;
        PerMinuteRate = perMinuteRate;
    }

    public decimal Calculate(TimeSpan duration)
    {
        if (duration < TimeSpan.Zero)
            throw new DomainException("Sürüş süresi negatif olamaz.");

        var billableMinutes = Math.Max(1, (long)Math.Ceiling(duration.TotalMinutes));
        return Math.Round(UnlockFee + billableMinutes * PerMinuteRate, 2, MidpointRounding.AwayFromZero);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return UnlockFee;
        yield return PerMinuteRate;
    }
}
