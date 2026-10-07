using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Scootly.Application.Abstractions;
using Scootly.Application.FieldOps.Commands;
using Scootly.Domain.FieldOps;
using Scootly.Domain.Fleet;

namespace Scootly.Worker.Jobs;

/// <summary>
/// Düşük batarya uyarılarının uzlaştırma adımı. Asıl yol olay tabanlıdır (telemetri eşiği aştığında
/// VehicleBatteryLow → <see cref="BatteryLowConsumer"/>); bu tarama olayın hiç üretilmediği (araç düşük bataryayla
/// kaydedildi) ya da kaybolduğu (DLQ) durumları yakalar ve eksik batarya görevlerini açar.
/// Yakın zamanda tamamlanmış bir batarya görevi olan araç, bekleme süresi dolana kadar atlanır: cihaz yeni
/// bataryayı henüz bildirmediyse aynı araç için hemen yeni görev açılmaz.
/// </summary>
public sealed class BatteryThresholdScanner : PeriodicJob
{
    public const int BatchSize = 200;

    private readonly WorkerOptions _options;

    public BatteryThresholdScanner(IServiceScopeFactory scopeFactory, IOptions<WorkerOptions> options, ILogger<BatteryThresholdScanner> logger)
        : base(scopeFactory, logger)
    {
        _options = options.Value;
    }

    protected override TimeSpan Interval => TimeSpan.FromMinutes(15);

    protected internal override async Task RunOnceAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var dbContext = services.GetRequiredService<IApplicationDbContext>();
        var cooldownStart = services.GetRequiredService<IClock>().UtcNow - _options.LowBatteryTaskCooldown;

        var lowBatteryCount = await dbContext.Vehicles
            .AsNoTracking()
            .CountAsync(v => v.Battery.Percentage < BatteryLevel.LowThresholdPercentage, cancellationToken);

        if (lowBatteryCount == 0)
            return;

        var vehiclesWithoutTask = await dbContext.Vehicles
            .AsNoTracking()
            .Where(v => v.Battery.Percentage < BatteryLevel.LowThresholdPercentage
                        && !dbContext.FieldTasks.Any(t => t.VehicleId == v.Id
                                                          && t.Type == FieldTaskType.BatteryReplacement
                                                          && (t.Status != FieldTaskStatus.Completed || t.CompletedAt > cooldownStart)))
            .OrderBy(v => v.Battery.Percentage)
            .Select(v => v.Id)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        var opened = 0;

        await ForEachInOwnScopeAsync(vehiclesWithoutTask, async (scopedServices, vehicleId, token) =>
        {
            var result = await scopedServices.GetRequiredService<FieldTaskCommandHandler>()
                .Handle(new CreateFieldTaskCommand(vehicleId, FieldTaskType.BatteryReplacement), token);

            if (result is { IsSuccess: true, Value: not null })
                opened++;
            else if (!result.IsSuccess)
                Logger.LogWarning("Batarya görevi açılamadı ({VehicleId}): {Error}", vehicleId, result.Error);
        }, cancellationToken);

        Logger.LogInformation(
            "Düşük bataryalı araç sayısı: {Count}; uzlaştırmada açılan batarya görevi: {Opened}",
            lowBatteryCount, opened);
    }
}
