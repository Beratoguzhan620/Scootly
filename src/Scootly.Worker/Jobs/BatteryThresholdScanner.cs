using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scootly.Application.Abstractions;
using Scootly.Application.IntegrationEvents;
using Scootly.Infrastructure.Persistence;

namespace Scootly.Worker.Jobs;

/// <summary>
/// Bataryası eşiğin altındaki araçları bulur ve her biri için olay yayınlar (64. gün).
/// </summary>
/// <remarks>
/// <para>
/// Önceki hali yalnızca log yazıyordu. Şimdi <c>vehicle.battery-low</c> olayı
/// yayınlıyor ve saha görevini açmak <c>BatteryLowConsumer</c>'ın işi. Tarayıcı
/// "ne oldu"yu söylüyor, "ne yapılacağına" karar vermiyor; yarın aynı olaya
/// bildirim gönderen ikinci bir tüketici eklemek bu sınıfa dokunmadan
/// yapılabilir.
/// </para>
/// <para>
/// <b>Bilinen tekrar:</b> her turda eşiğin altındaki BÜTÜN araçlar için olay
/// yayınlanıyor, önceki turda bildirilmiş olsalar bile. Aynı araç için ikinci
/// görevin açılmasını tüketici engelliyor, ama kuyruk beş dakikada bir aynı
/// olaylarla doluyor. Teknik borç listesinde.
/// </para>
/// <para>
/// Tur gövdesi try/catch içinde: RabbitMQ ya da veritabanı kısa süre
/// erişilemezse bu servis çökmemeli — .NET 8'den beri kaçan bir istisna bütün
/// Worker'ı, dolayısıyla tüketicileri de durduruyor.
/// </para>
/// </remarks>
public sealed class BatteryThresholdScanner : BackgroundService
{
    private const int LowBatteryThreshold = 20;
    private static readonly TimeSpan TurAraligi = TimeSpan.FromMinutes(5);

    private readonly IServiceProvider _services;
    private readonly IEventPublisher _publisher;
    private readonly ILogger<BatteryThresholdScanner> _logger;

    public BatteryThresholdScanner(
        IServiceProvider services,
        IEventPublisher publisher,
        ILogger<BatteryThresholdScanner> logger)
    {
        _services = services;
        _publisher = publisher;
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
                var clock = scope.ServiceProvider.GetRequiredService<IClock>();

                var lowBatteryVehicles = await dbContext.Vehicles
                    .AsNoTracking()
                    .Where(v => v.Battery.Percentage < LowBatteryThreshold)
                    .Select(v => new { v.Id, v.Battery.Percentage })
                    .ToListAsync(stoppingToken);

                foreach (var vehicle in lowBatteryVehicles)
                {
                    await _publisher.PublishAsync(
                        new VehicleBatteryLowIntegrationEvent(
                            EventId: Guid.NewGuid(),
                            OccurredOnUtc: clock.UtcNow,
                            VehicleId: vehicle.Id,
                            BatteryPercentage: vehicle.Percentage),
                        stoppingToken);
                }

                if (lowBatteryVehicles.Count > 0)
                {
                    _logger.LogInformation(
                        "Batarya taramasi: {Adet} arac icin vehicle.battery-low yayinlandi.", lowBatteryVehicles.Count);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Batarya taramasi bu turda basarisiz oldu; bir sonraki turda tekrar denenecek.");
            }

            try
            {
                await Task.Delay(TurAraligi, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
