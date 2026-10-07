using System.Diagnostics;

namespace Scootly.Infrastructure.Observability;

/// <summary>
/// Outbox yayını (producer) ve kuyruk tüketimi (consumer) span'leri için paylaşılan kaynak.
/// AddScootlyTelemetry bu kaynağı dinlemeye kaydediyor; Otel yapılandırılmamışsa StartActivity null döner (no-op).
/// </summary>
public static class ScootlyActivitySource
{
    public const string Name = "Scootly";

    public static readonly ActivitySource Instance = new(Name);
}
