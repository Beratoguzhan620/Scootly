using System.Diagnostics.Metrics;

namespace Scootly.Infrastructure.Observability;

/// <summary>
/// Uygulamaya özgü metrikler. Etiketlere kullanıcı/araç kimliği KONMAZ (kardinalite patlar).
/// Otel yapılandırılmamışsa Add çağrıları dinleyici olmadığı için etkisiz kalır.
/// </summary>
public static class ScootlyMetrics
{
    public const string MeterName = "Scootly";

    private static readonly Meter MeterInstance = new(MeterName);

    private static long _outboxPending;

    public static readonly Counter<long> RidesStarted = MeterInstance.CreateCounter<long>(
        "scootly.rides.started", unit: "{ride}", description: "Başlatılan sürüş sayısı.");

    public static readonly Counter<long> RidesCompleted = MeterInstance.CreateCounter<long>(
        "scootly.rides.completed", unit: "{ride}", description: "Tamamlanan sürüş sayısı.");

    public static readonly ObservableGauge<long> OutboxPending = MeterInstance.CreateObservableGauge(
        "scootly.outbox.pending",
        () => Interlocked.Read(ref _outboxPending),
        unit: "{message}",
        description: "Outbox'ta yayınlanmayı bekleyen kayıt sayısı.");

    public static void SetOutboxPending(long count) => Interlocked.Exchange(ref _outboxPending, count);
}
