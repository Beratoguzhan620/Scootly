using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Scootly.Infrastructure.Observability;

public static class TelemetryExtensions
{
    // Varsayılan 60 sn; geliştirmede sonucu beklemek zor olduğu için kısaltıldı.
    private const int MetricExportIntervalMilliseconds = 15_000;

    /// <summary>
    /// ASP.NET Core, HttpClient ve Npgsql izlerini ile uygulama metriklerini OTLP üzerinden Collector'a gönderir.
    /// Otel:Endpoint boşsa telemetri sessizce devre dışı kalır.
    /// </summary>
    public static IServiceCollection AddScootlyTelemetry(this IServiceCollection services, IConfiguration configuration, string serviceName)
    {
        var endpoint = configuration[$"{TelemetryOptions.SectionName}:Endpoint"];

        if (string.IsNullOrWhiteSpace(endpoint))
            return services;

        var collectorUri = new Uri(endpoint);

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing => tracing
                .AddSource(ScootlyActivitySource.Name)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddNpgsql()
                .AddOtlpExporter(otlp => otlp.Endpoint = collectorUri))
            .WithMetrics(metrics => metrics
                .AddMeter(ScootlyMetrics.MeterName)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddOtlpExporter((otlp, reader) =>
                {
                    otlp.Endpoint = collectorUri;
                    reader.PeriodicExportingMetricReaderOptions.ExportIntervalMilliseconds = MetricExportIntervalMilliseconds;
                }));

        return services;
    }
}