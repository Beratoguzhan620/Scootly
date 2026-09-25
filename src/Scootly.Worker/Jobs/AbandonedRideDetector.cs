using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scootly.Domain.Riding;
using Scootly.Infrastructure.Persistence;

namespace Scootly.Worker.Jobs;

public sealed class AbandonedRideDetector : BackgroundService
{
    private const int AbandonedThresholdHours = 2;
    private readonly IServiceProvider _services;
    private readonly ILogger<AbandonedRideDetector> _logger;

    public AbandonedRideDetector(IServiceProvider services, ILogger<AbandonedRideDetector> logger)
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

                var cutoff = DateTime.UtcNow.AddHours(-AbandonedThresholdHours);

                var staleRides = await dbContext.Rides
                    .Where(r => r.Status == RideStatus.Active && r.StartedAt < cutoff)
                    .ToListAsync(stoppingToken);

                foreach (var ride in staleRides)
                {
                    ride.Abandon();
                    _logger.LogWarning("Sürüş terk edildi olarak işaretlendi: {RideId}", ride.Id);
                }

                if (staleRides.Count > 0)
                    await dbContext.SaveChangesAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Uygulama kapanıyor, normal.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "AbandonedRideDetector turunda beklenmeyen hata oluştu.");
            }

            await Task.Delay(TimeSpan.FromMinutes(10), stoppingToken);
        }
    }
}