namespace Scootly.Domain.Riding;

public static class ReservationPolicy
{
    public static readonly TimeSpan Duration = TimeSpan.FromMinutes(10);

    /// <summary>Bu andan önce yapılmış rezervasyonların süresi dolmuştur.</summary>
    public static DateTime ExpiryCutoff(DateTime now) => now - Duration;

    public static bool IsExpired(DateTime reservedAt, DateTime now) => now >= reservedAt + Duration;
}
