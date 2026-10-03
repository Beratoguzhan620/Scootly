using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Scootly.Infrastructure.Observability;

public static class TelemetryExtensions
{
    /// <summary>
    /// ASP.NET Core, HttpClient ve Npgsql izlerini toplar, yapılandırılmışsa OTLP üzerinden
    /// Collector'a (ve oradan Jaeger'a) gönderir. Otel:Endpoint boşsa izleme sessizce devre dışı kalır.
    /// </summary>
    public static IServiceCollection AddScootlyTelemetry(this IServiceCollection services, IConfiguration configuration, string serviceName)
    {
        var endpoint = configuration[$"{TelemetryOptions.SectionName}:Endpoint"];

        if (string.IsNullOrWhiteSpace(endpoint))
            return services;

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(serviceName))
            .WithTracing(tracing =>
            {
                tracing
    .AddSource(ScootlyActivitySource.Name)
    .AddAspNetCoreInstrumentation()
    .AddHttpClientInstrumentation()
    .AddNpgsql()
    .AddOtlpExporter(otlp => otlp.Endpoint = new Uri(endpoint));
            });

        return services;
    }
}