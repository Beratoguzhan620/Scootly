using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Scootly.Infrastructure.Persistence;

namespace Scootly.Infrastructure.Observability;

/// <summary>
/// ObservableGauge geri çağrısı eşzamanlı çalışır, içinde veritabanı sorgusu yapılmaz.
/// Bu servis sayıyı periyodik olarak okuyup bellekteki değere yazar, gauge o değeri raporlar.
/// </summary>
public sealed class OutboxMetricsCollector : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(10);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxMetricsCollector> _logger;

    public OutboxMetricsCollector(IServiceScopeFactory scopeFactory, ILogger<OutboxMetricsCollector> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<ScootlyDbContext>();

                var pending = await dbContext.OutboxMessages
                    .AsNoTracking()
                    .LongCountAsync(m => m.ProcessedAt == null, stoppingToken);

                ScootlyMetrics.SetOutboxPending(pending);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Outbox bekleyen kayıt sayısı okunamadı.");
            }

            try
            {
                await Task.Delay(Interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
