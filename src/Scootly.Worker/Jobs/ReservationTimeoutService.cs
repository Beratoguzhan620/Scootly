using Microsoft.EntityFrameworkCore;
using Scootly.Application.Abstractions;
using Scootly.Application.Riding.Commands;
using Scootly.Domain.Fleet;
using Scootly.Domain.Riding;
using Scootly.Infrastructure.Caching;

namespace Scootly.Worker.Jobs;

public sealed class ReservationTimeoutService : PeriodicJob
{
    public ReservationTimeoutService(IServiceScopeFactory scopeFactory, ILogger<ReservationTimeoutService> logger)
        : base(scopeFactory, logger)
    {
    }

    protected override TimeSpan Interval => TimeSpan.FromSeconds(30);

    protected override async Task RunOnceAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var dbContext = services.GetRequiredService<IApplicationDbContext>();
        var clock = services.GetRequiredService<IClock>();

        var cutoff = ReservationPolicy.ExpiryCutoff(clock.UtcNow);

        var expiredVehicleIds = await dbContext.Vehicles
            .AsNoTracking()
            .Where(v => v.Status == VehicleStatus.Reserved && v.ReservedAt != null && v.ReservedAt <= cutoff)
            .Select(v => v.Id)
            .ToListAsync(cancellationToken);

        if (expiredVehicleIds.Count == 0)
            return;

        var expired = 0;

        await ForEachInOwnScopeAsync(expiredVehicleIds, async (scopedServices, vehicleId, token) =>
        {
            var result = await scopedServices.GetRequiredService<ExpireReservationCommandHandler>()
                .Handle(new ExpireReservationCommand(vehicleId), token);

            if (result.IsSuccess)
            {
                expired++;
                Logger.LogInformation("Süresi dolmuş rezervasyon kaldırıldı: {VehicleId}", vehicleId);
            }
            else
            {
                Logger.LogInformation("Rezervasyon kaldırılamadı ({VehicleId}): {Error}", vehicleId, result.Error);
            }
        }, cancellationToken);

        if (expired > 0)
            await services.GetRequiredService<NearbyVehicleCache>().InvalidateAsync(CacheKeys.NearbyVehiclesDefault(), cancellationToken);
    }
}
