using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using Scootly.Infrastructure.Caching;
using Scootly.Infrastructure.Messaging;
using StackExchange.Redis;

namespace Scootly.Infrastructure.HealthChecks;

public sealed class RabbitMqHealthCheck : IHealthCheck
{
    private readonly RabbitMqConnectionProvider _connectionProvider;

    public RabbitMqHealthCheck(RabbitMqConnectionProvider connectionProvider)
    {
        _connectionProvider = connectionProvider;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var connection = await _connectionProvider.GetConnectionAsync(cancellationToken);
            return connection.IsOpen
                ? HealthCheckResult.Healthy()
                : new HealthCheckResult(context.Registration.FailureStatus, "RabbitMQ bağlantısı kapalı.");
        }
        catch (Exception ex)
        {
            return new HealthCheckResult(context.Registration.FailureStatus, "RabbitMQ'ya bağlanılamadı.", ex);
        }
    }
}

public sealed class RedisHealthCheck : IHealthCheck
{
    private readonly IServiceProvider _services;
    private readonly IOptions<RedisOptions> _options;

    public RedisHealthCheck(IServiceProvider services, IOptions<RedisOptions> options)
    {
        _services = services;
        _options = options;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.Value.ConnectionString))
            return HealthCheckResult.Healthy("Redis yapılandırılmadı; süreç içi önbellek kullanılıyor.");

        try
        {
            var latency = await _services.GetRequiredService<IConnectionMultiplexer>().GetDatabase().PingAsync();
            return HealthCheckResult.Healthy($"Gecikme: {latency.TotalMilliseconds:F1} ms");
        }
        catch (Exception ex)
        {
            // Önbellek best-effort olduğu için Redis kesintisi hizmeti durdurmaz: "Degraded".
            return HealthCheckResult.Degraded("Redis'e ulaşılamadı.", ex);
        }
    }
}
