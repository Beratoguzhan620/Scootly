using Microsoft.Extensions.DependencyInjection;
using Scootly.Application.Telemetry;
using Scootly.Infrastructure.Persistence;

namespace Scootly.Api.Services;

public sealed class TelemetryChannelConsumer : BackgroundService
{
    private readonly TelemetryChannel _telemetryChannel;
    private readonly IServiceProvider _services;
    private readonly ILogger<TelemetryChannelConsumer> _logger;

    public TelemetryChannelConsumer(
        TelemetryChannel telemetryChannel,
        IServiceProvider services,
        ILogger<TelemetryChannelConsumer> logger)
    {
        _telemetryChannel = telemetryChannel;
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await foreach (var reading in _telemetryChannel.ReadAllAsync(stoppingToken))
            {
                try
                {
                    using var scope = _services.CreateScope();
                    var dbContext = scope.ServiceProvider.GetRequiredService<ScootlyDbContext>();

                    dbContext.TelemetryReadings.Add(reading);
                    await dbContext.SaveChangesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Telemetri kaydı yazılırken hata oluştu: {VehicleId}", reading.VehicleId);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Uygulama kapanıyor, normal.
        }
    }
}