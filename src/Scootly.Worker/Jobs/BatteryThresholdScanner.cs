using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scootly.Infrastructure.Persistence;

namespace Scootly.Worker.Jobs;

public sealed class BatteryThresholdScanner : BackgroundService
{
    private const int LowBatteryThreshold = 20;
    private readonly IServiceProvider _services;
    private readonly ILogger<BatteryThresholdScanner> _logger;

    public BatteryThresholdScanner(IServiceProvider services, ILogger<BatteryThresholdScanner> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _services.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ScootlyDbContext>();

                var lowBatteryVehicles = await dbContext.Vehicles
                    .AsNoTracking()
                    .Where(v => v.Battery.Percentage < LowBatteryThreshold)
                    .Select(v => new { v.Id, v.Battery.Percentage })
                    .ToListAsync(stoppingToken);

                foreach (var vehicle in lowBatteryVehicles)
                {
                    _logger.LogWarning(
                        "Batarya düşük: {VehicleId} — %{Percentage}", vehicle.Id, vehicle.Percentage);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Uygulama kapanıyor, normal.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "BatteryThresholdScanner turunda beklenmeyen hata oluştu.");
            }

            await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken);
        }
    }
}