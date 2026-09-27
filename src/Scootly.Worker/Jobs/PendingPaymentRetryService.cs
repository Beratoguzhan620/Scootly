using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Scootly.Application.Abstractions;
using Scootly.Application.Payments.Commands;
using Scootly.Domain.Riding;

namespace Scootly.Worker.Jobs;

/// <summary>
/// Ödeme saga'sının uzlaştırma (reconciliation) adımı:
/// <list type="bullet">
/// <item>reddedilmiş ödemeleri geri çekilme süresi dolunca tekrar dener (azami deneme sayısına kadar),</item>
/// <item>olay mesajı herhangi bir nedenle kaybolmuş, hiç denenmemiş ödemeleri de yakalar.</item>
/// </list>
/// Tahsilat idempotent olduğu için tüketiciyle aynı sürüşü aynı anda denemesi çift tahsilat oluşturmaz.
/// </summary>
public sealed class PendingPaymentRetryService : PeriodicJob
{
    private const int BatchSize = 50;

    private readonly WorkerOptions _options;

    public PendingPaymentRetryService(IServiceScopeFactory scopeFactory, IOptions<WorkerOptions> options, ILogger<PendingPaymentRetryService> logger)
        : base(scopeFactory, logger)
    {
        _options = options.Value;
    }

    protected override TimeSpan Interval => TimeSpan.FromMinutes(1);

    protected override async Task RunOnceAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var dbContext = services.GetRequiredService<IApplicationDbContext>();
        var now = services.GetRequiredService<IClock>().UtcNow;

        var retryBefore = now - _options.PaymentRetryBackoff;
        var neverAttemptedBefore = now - _options.UnchargedRideGracePeriod;

        var dueRideIds = await dbContext.Rides
            .AsNoTracking()
            .Where(r => r.PaymentStatus == PaymentStatus.Pending
                        && ((r.LastPaymentAttemptAt != null && r.LastPaymentAttemptAt < retryBefore)
                            || (r.LastPaymentAttemptAt == null && r.EndedAt < neverAttemptedBefore)))
            .OrderBy(r => r.EndedAt)
            .Select(r => r.Id)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        await ForEachInOwnScopeAsync(dueRideIds, async (scopedServices, rideId, token) =>
        {
            var result = await scopedServices.GetRequiredService<ChargeRideCommandHandler>().Handle(new ChargeRideCommand(rideId), token);

            if (!result.IsSuccess)
            {
                Logger.LogWarning("Bekleyen ödeme denenemedi ({RideId}): {Error}", rideId, result.Error);
                return;
            }

            Logger.LogInformation("Bekleyen ödeme denemesi ({RideId}): {Outcome}", rideId, result.Value);
        }, cancellationToken);
    }
}
