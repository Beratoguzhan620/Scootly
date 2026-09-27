namespace Scootly.PaymentSimulator;

/// <summary>
/// Dayanıklılık deneyleri için hata enjeksiyonu:
/// "decline" kalıcı iş reddi (402), "outage" geçici servis hatası (503) üretir.
/// </summary>
public sealed class FailureInjector
{
    private int _declineRatePercent;
    private int _outageRatePercent;

    public int DeclineRatePercent => Volatile.Read(ref _declineRatePercent);
    public int OutageRatePercent => Volatile.Read(ref _outageRatePercent);

    public void Configure(int declineRatePercent, int outageRatePercent)
    {
        Volatile.Write(ref _declineRatePercent, Math.Clamp(declineRatePercent, 0, 100));
        Volatile.Write(ref _outageRatePercent, Math.Clamp(outageRatePercent, 0, 100));
    }

    public bool ShouldDecline() => Random.Shared.Next(0, 100) < DeclineRatePercent;

    public bool ShouldSimulateOutage() => Random.Shared.Next(0, 100) < OutageRatePercent;
}
