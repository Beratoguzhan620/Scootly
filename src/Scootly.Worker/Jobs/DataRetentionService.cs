using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Scootly.Application.Abstractions;
using Scootly.Infrastructure.Persistence;

namespace Scootly.Worker.Jobs;

/// <summary>
/// Veri saklama politikası (ADR 0004, KVKK/GDPR "gereğinden fazla veri tutmama" ilkesi):
/// eski telemetri silinir, eski sürüşlerin konumları anonimleştirilir, işlenmiş mesaj kayıtları temizlenir.
/// </summary>
public sealed class DataRetentionService : PeriodicJob
{
    private readonly WorkerOptions _options;

    public DataRetentionService(IServiceScopeFactory scopeFactory, IOptions<WorkerOptions> options, ILogger<DataRetentionService> logger)
        : base(scopeFactory, logger)
    {
        _options = options.Value;
    }

    protected override TimeSpan Interval => TimeSpan.FromHours(6);

    protected override async Task RunOnceAsync(IServiceProvider services, CancellationToken cancellationToken)
    {
        var dbContext = services.GetRequiredService<ScootlyDbContext>();
        var now = services.GetRequiredService<IClock>().UtcNow;

        var telemetryCutoff = now.AddDays(-_options.TelemetryRetentionDays);
        var deletedTelemetry = await dbContext.TelemetryReadings
            .Where(t => t.RecordedAt < telemetryCutoff)
            .ExecuteDeleteAsync(cancellationToken);

        var rideLocationCutoff = now.AddDays(-_options.RideLocationRetentionDays);
        var anonymizedRides = await dbContext.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Rides"
            SET "StartLatitude" = NULL, "StartLongitude" = NULL, "EndLatitude" = NULL, "EndLongitude" = NULL
            WHERE "EndedAt" < {rideLocationCutoff}
              AND ("StartLatitude" IS NOT NULL OR "EndLatitude" IS NOT NULL)
            """, cancellationToken);

        var outboxCutoff = now.AddDays(-_options.OutboxRetentionDays);
        var deletedOutbox = await dbContext.OutboxMessages
            .Where(m => m.ProcessedAt != null && m.ProcessedAt < outboxCutoff)
            .ExecuteDeleteAsync(cancellationToken);

        var processedCutoff = now.AddDays(-_options.ProcessedMessageRetentionDays);
        var deletedProcessed = await dbContext.ProcessedMessages
            .Where(m => m.ProcessedAt < processedCutoff)
            .ExecuteDeleteAsync(cancellationToken);

        if (deletedTelemetry + anonymizedRides + deletedOutbox + deletedProcessed > 0)
        {
            Logger.LogInformation(
                "Saklama politikası uygulandı: {Telemetry} telemetri silindi, {Rides} sürüş anonimleştirildi, " +
                "{Outbox} outbox ve {Processed} işlenmiş mesaj kaydı temizlendi.",
                deletedTelemetry, anonymizedRides, deletedOutbox, deletedProcessed);
        }
    }
}
