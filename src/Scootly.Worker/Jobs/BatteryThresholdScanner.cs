using Microsoft.EntityFrameworkCore;
using Scootly.Application.Abstractions;
using Scootly.Domain.Fleet;

namespace Scootly.Worker.Jobs;

/// <summary>
/// Düşük bataryalı araçların periyodik özeti. Tekil uyarılar olay tabanlıdır (telemetri eşiği aştığında
/// VehicleBatteryLow olayı → <see cref="BatteryLowConsumer"/>); bu tarama yalnızca genel görünümü raporlar.
/// </summary>
public sealed class BatteryThresholdScanner : PeriodicJob
{
    public BatteryThresholdScanner(IServiceScopeFactory scopeFactory, ILogger<BatteryThresholdScanner> logger)
        : base(scopeFactory, logger)
    {
    }

    protected override TimeSpan Interval => TimeSpan.FromMinutes(15);

    protected override async Task RunOnceAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var dbContext = services.GetRequiredService<IApplicationDbContext>();

        var lowBatteryCount = await dbContext.Vehicles
            .AsNoTracking()
            .CountAsync(v => v.Battery.Percentage < BatteryLevel.LowThresholdPercentage
                             && v.Status != VehicleStatus.Maintenance, cancellationToken);

        if (lowBatteryCount > 0)
            Logger.LogInformation("Hizmetteki düşük bataryalı araç sayısı: {Count}", lowBatteryCount);
    }
}
