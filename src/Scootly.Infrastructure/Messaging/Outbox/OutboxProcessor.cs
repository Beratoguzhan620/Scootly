using System.Diagnostics;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using Scootly.Application.Abstractions;
using Scootly.Infrastructure.Observability;
using Scootly.Infrastructure.Persistence;

namespace Scootly.Infrastructure.Messaging.Outbox;

/// <summary>
/// Outbox'taki yayınlanmamış mesajları RabbitMQ'ya aktarır.
/// <list type="bullet">
/// <item>Satırlar <c>FOR UPDATE SKIP LOCKED</c> ile kilitlenir: birden fazla süreç aynı mesajı yayınlamaz.</item>
/// <item>Mesajlar kalıcı (persistent) işaretlenir ve publisher confirm beklenir: broker onaylamadan "işlendi" sayılmaz.</item>
/// <item>Bir mesaj yayınlanamazsa sıra korunarak parti orada kesilir, hata mesajın üzerine yazılır ve
/// <see cref="OutboxPublishException"/> fırlatılır; böylece yayıncı servisi artan beklemeyle tekrar dener.</item>
/// <item>Aynı mesaj <see cref="StuckMessageAlertThreshold"/> kez başarısız olursa her denemede Error seviyesinde
/// loglanır. Mesaj karantinaya alınmaz: uzun bir broker kesintisinde olayları atmak, sırayı korumaktan daha pahalıdır.</item>
/// </list>
/// </summary>
public sealed class OutboxProcessor
{
    public const int StuckMessageAlertThreshold = 10;

    private readonly ScootlyDbContext _dbContext;
    private readonly RabbitMqConnectionProvider _connectionProvider;
    private readonly IClock _clock;
    private readonly MessagingOptions _options;
    private readonly ILogger<OutboxProcessor> _logger;

    public OutboxProcessor(
        ScootlyDbContext dbContext,
        RabbitMqConnectionProvider connectionProvider,
        IClock clock,
        IOptions<MessagingOptions> options,
        ILogger<OutboxProcessor> logger)
    {
        _dbContext = dbContext;
        _connectionProvider = connectionProvider;
        _clock = clock;
        _options = options.Value;
        _logger = logger;
    }

    /// <returns>Bu turda yayınlanan mesaj sayısı.</returns>
    public async Task<int> PublishPendingAsync(CancellationToken cancellationToken)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var batchSize = _options.OutboxBatchSize;
        var messages = await _dbContext.OutboxMessages
            .FromSqlInterpolated($"""
                SELECT * FROM "OutboxMessages"
                WHERE "ProcessedAt" IS NULL
                ORDER BY "CreatedAt"
                LIMIT {batchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);

        if (messages.Count == 0)
        {
            await transaction.CommitAsync(cancellationToken);
            return 0;
        }

        var connection = await _connectionProvider.GetConnectionAsync(cancellationToken);

        await using var channel = await connection.CreateChannelAsync(
            new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
            cancellationToken);

        await channel.ExchangeDeclareAsync(MessagingTopology.EventsExchange, ExchangeType.Topic, durable: true, cancellationToken: cancellationToken);

        var published = 0;
        Exception? failure = null;
        OutboxMessage? failedMessage = null;

        foreach (var message in messages)
        {
            var parentContext = ParseTraceParent(message.TraceParent);

            using var activity = ScootlyActivitySource.Instance.StartActivity(
                $"publish {message.EventType}", ActivityKind.Producer, parentContext);

            activity?.SetTag("messaging.system", "rabbitmq");
            activity?.SetTag("messaging.destination", MessagingTopology.EventsExchange);
            activity?.SetTag("messaging.message_id", message.Id.ToString());

            try
            {
                var properties = new BasicProperties
                {
                    Persistent = true,
                    MessageId = message.Id.ToString(),
                    Type = message.EventType,
                    ContentType = "application/json",
                    Timestamp = new AmqpTimestamp(new DateTimeOffset(message.CreatedAt, TimeSpan.Zero).ToUnixTimeSeconds()),
                    Headers = activity is not null
                        ? new Dictionary<string, object?> { ["traceparent"] = activity.Id }
                        : null
                };

                // Publisher confirm etkin olduğu için bu çağrı broker onayını bekler; nack durumunda istisna fırlatır.
                await channel.BasicPublishAsync(
                    MessagingTopology.EventsExchange,
                    message.EventType,
                    mandatory: false,
                    properties,
                    Encoding.UTF8.GetBytes(message.Payload),
                    cancellationToken);

                message.MarkAsProcessed(_clock.UtcNow);
                published++;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                activity?.SetStatus(ActivityStatusCode.Error, ex.Message);
                message.RecordFailedAttempt(ex.Message);
                failure = ex;
                failedMessage = message;
                break;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        if (published > 0)
            _logger.LogInformation("{Count} outbox mesajı yayınlandı.", published);

        if (failure is not null && failedMessage is not null)
        {
            if (failedMessage.Attempts >= StuckMessageAlertThreshold)
            {
                _logger.LogError(
                    "Outbox mesajı {Attempts} denemedir yayınlanamıyor, arkasındaki mesajlar bekliyor: {EventType} ({MessageId})",
                    failedMessage.Attempts, failedMessage.EventType, failedMessage.Id);
            }

            throw new OutboxPublishException(failedMessage.Id, published, failure);
        }

        return published;
    }

    private static ActivityContext ParseTraceParent(string? traceParent)
    {
        if (string.IsNullOrWhiteSpace(traceParent))
            return default;

        return ActivityContext.TryParse(traceParent, null, out var context) ? context : default;
    }
}
