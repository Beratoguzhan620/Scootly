namespace Scootly.PaymentSimulator;

public static class FailureInjector
{
    private static int _failureRatePercent = 0;
    private static readonly Random Random = new();

    public static void SetFailureRate(int percent)
    {
        _failureRatePercent = Math.Clamp(percent, 0, 100);
    }

    public static bool ShouldFail()
    {
        return Random.Next(0, 100) < _failureRatePercent;
    }
}