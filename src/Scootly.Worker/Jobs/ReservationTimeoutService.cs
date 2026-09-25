using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scootly.Application.Riding.Commands;
using Scootly.Domain.Fleet;
using Scootly.Infrastructure.Persistence;

namespace Scootly.Worker.Jobs;

public sealed class ReservationTimeoutService : BackgroundService
{
    private const int ReservationDurationMinutes = 10;
    private readonly IServiceProvider _services;
    private readonly ILogger<ReservationTimeoutService> _logger;

    public ReservationTimeoutService(IServiceProvider services, ILogger<ReservationTimeoutService> logger)
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
                var handler = scope.ServiceProvider.GetRequiredService<CancelReservationCommandHandler>();

                var cutoff = DateTime.UtcNow.AddMinutes(-ReservationDurationMinutes);

                var expiredVehicles = await dbContext.Vehicles
                    .Where(v => v.Status == VehicleStatus.Reserved && v.ReservedAt != null && v.ReservedAt < cutoff)
                    .Select(v => v.Id)
                    .ToListAsync(stoppingToken);

                foreach (var vehicleId in expiredVehicles)
                {
                    var command = new CancelReservationCommand(vehicleId);
                    var result = await handler.Handle(command, stoppingToken);

                    if (result.IsSuccess)
                        _logger.LogInformation("Süresi dolmuş rezervasyon iptal edildi: {VehicleId}", vehicleId);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Uygulama kapanıyor, normal.
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "ReservationTimeoutService turunda beklenmeyen hata oluştu.");
            }

            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
    }
}