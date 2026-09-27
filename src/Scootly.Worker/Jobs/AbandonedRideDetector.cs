using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Scootly.Application.Abstractions;
using Scootly.Application.Riding.Commands;
using Scootly.Domain.Riding;
using Scootly.Infrastructure.Caching;

namespace Scootly.Worker.Jobs;

/// <summary>
/// Eşik süreyi aşan aktif sürüşleri kapatır: sürüş ücretlendirilir ve araç, kilitli kalmak yerine
/// saha kontrolü için bakıma alınır.
/// </summary>
public sealed class AbandonedRideDetector : PeriodicJob
{
    private readonly WorkerOptions _options;

    public AbandonedRideDetector(IServiceScopeFactory scopeFactory, IOptions<WorkerOptions> options, ILogger<AbandonedRideDetector> logger)
        : base(scopeFactory, logger)
    {
        _options = options.Value;
    }

    protected override TimeSpan Interval => TimeSpan.FromMinutes(5);

    protected override async Task RunOnceAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var dbContext = services.GetRequiredService<IApplicationDbContext>();
        var clock = services.GetRequiredService<IClock>();

        var cutoff = clock.UtcNow - _options.AbandonedRideThreshold;

        var staleRideIds = await dbContext.Rides
            .AsNoTracking()
            .Where(r => r.Status == RideStatus.Active && r.StartedAt < cutoff)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        if (staleRideIds.Count == 0)
            return;

        var abandoned = 0;

        await ForEachInOwnScopeAsync(staleRideIds, async (scopedServices, rideId, token) =>
        {
            var result = await scopedServices.GetRequiredService<AbandonRideCommandHandler>().Handle(new AbandonRideCommand(rideId), token);

            if (result.IsSuccess)
            {
                abandoned++;
                Logger.LogWarning("Sürüş terk edildi olarak kapatıldı, araç bakıma alındı: {RideId}", rideId);
            }
            else
            {
                Logger.LogInformation("Sürüş kapatılamadı ({RideId}): {Error}", rideId, result.Error);
            }
        }, cancellationToken);

        if (abandoned > 0)
            await services.GetRequiredService<NearbyVehicleCache>().InvalidateAsync(CacheKeys.NearbyVehiclesDefault(), cancellationToken);
    }
}
