namespace Scootly.Infrastructure.Observability;

public sealed class TelemetryOptions
{
    public const string SectionName = "Otel";

    public string? Endpoint { get; set; }
}
