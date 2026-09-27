using System.Text;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace Scootly.Infrastructure.Messaging.Consumers;

public enum ConsumeResult
{
    Ack,

    /// <summary>Geçici hata: mesaj retry kuyruğunda bekletilip yeniden denenir.</summary>
    Retry,

    /// <summary>Kalıcı hata (örn. bozuk içerik): yeniden denemeden doğrudan DLQ'ya taşınır.</summary>
    DeadLetter
}

public sealed record ReceivedMessage(string RoutingKey, string? MessageId, string Body);

/// <summary>
/// Tüm RabbitMQ tüketicileri için ortak altyapı:
/// bağlantı kurulamazsa artan beklemeyle tekrar dener (servis sessizce devre dışı kalmaz),
/// prefetch sınırı uygular, gecikmeli retry kuyruğu ve DLQ topolojisini kurar,
/// her mesajı kendi DI scope'unda işler.
/// </summary>
public abstract class RabbitMqConsumerService : BackgroundService
{
    public const string DeadLetterReasonHeader = "x-scootly-dead-letter-reason";

    private static readonly TimeSpan MaxReconnectDelay = TimeSpan.FromSeconds(60);
    private static readonly TimeSpan ChannelHealthCheckInterval = TimeSpan.FromSeconds(5);

    private readonly RabbitMqConnectionProvider _connectionProvider;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MessagingOptions _options;

    protected RabbitMqConsumerService(
        RabbitMqConnectionProvider connectionProvider,
        IServiceScopeFactory scopeFactory,
        IOptions<MessagingOptions> options,
        ILogger logger)
    {
        _connectionProvider = connectionProvider;
        _scopeFactory = scopeFactory;
        _options = options.Value;
        Logger = logger;
    }

    protected ILogger Logger { get; }

    /// <summary>Kalıcı kuyruk adı; <see cref="IsDurable"/> false ise yalnızca log amaçlıdır.</summary>
    protected abstract string QueueName { get; }

    protected abstract IReadOnlyCollection<string> RoutingKeys { get; }

    /// <summary>
    /// true: paylaşımlı kalıcı kuyruk (rekabetçi tüketim, retry + DLQ).
    /// false: her süreç için geçici, özel kuyruk (yayın/fan-out; yeniden deneme yok).
    /// </summary>
    protected virtual bool IsDurable => true;

    protected abstract Task<ConsumeResult> HandleAsync(ReceivedMessage message, IServiceProvider services, CancellationToken cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reconnectDelay = TimeSpan.FromSeconds(1);

        while (!stoppingToken.IsCancellationRequested)
        {
            IChannel? channel = null;

            try
            {
                var connection = await _connectionProvider.GetConnectionAsync(stoppingToken);

                channel = await connection.CreateChannelAsync(
                    new CreateChannelOptions(publisherConfirmationsEnabled: true, publisherConfirmationTrackingEnabled: true),
                    stoppingToken);

                var queue = await DeclareTopologyAsync(channel, stoppingToken);
                await channel.BasicQosAsync(prefetchSize: 0, prefetchCount: _options.PrefetchCount, global: false, stoppingToken);

                var consumer = new AsyncEventingBasicConsumer(channel);
                var consumingChannel = channel;
                consumer.ReceivedAsync += (_, args) => OnReceivedAsync(consumingChannel, queue, args, stoppingToken);

                await channel.BasicConsumeAsync(queue, autoAck: false, consumer, stoppingToken);

                Logger.LogInformation("{Consumer} dinlemeye başladı: {Queue}", GetType().Name, queue);
                reconnectDelay = TimeSpan.FromSeconds(1);

                // Bağlantı kesintilerini istemci kütüphanesi kendisi kurtarır; burada yalnızca kanal düzeyindeki
                // (bağlantı açıkken kanalın kapanması gibi) kurtarılamayan durumlar için kanal yeniden kurulur.
                while (!stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(ChannelHealthCheckInterval, stoppingToken);

                    if (channel.IsClosed && connection.IsOpen)
                    {
                        Logger.LogWarning("{Consumer} kanalı kapandı, yeniden kuruluyor.", GetType().Name);
                        break;
                    }
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "{Consumer} başlatılamadı; {Delay} sonra tekrar denenecek.", GetType().Name, reconnectDelay);

                try
                {
                    await Task.Delay(reconnectDelay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                reconnectDelay = TimeSpan.FromSeconds(Math.Min(reconnectDelay.TotalSeconds * 2, MaxReconnectDelay.TotalSeconds));
            }
            finally
            {
                if (channel is not null)
                    await DisposeQuietlyAsync(channel);
            }
        }
    }

    private async Task<string> DeclareTopologyAsync(IChannel channel, CancellationToken cancellationToken)
    {
        await channel.ExchangeDeclareAsync(MessagingTopology.EventsExchange, ExchangeType.Topic, durable: true, cancellationToken: cancellationToken);

        string queue;

        if (IsDurable)
        {
            var retryQueue = MessagingTopology.RetryQueueFor(QueueName);
            var deadLetterQueue = MessagingTopology.DeadLetterQueueFor(QueueName);

            await channel.QueueDeclareAsync(deadLetterQueue, durable: true, exclusive: false, autoDelete: false, cancellationToken: cancellationToken);

            // Retry kuyruğunda TTL dolan mesaj, varsayılan exchange üzerinden ana kuyruğa geri döner.
            await channel.QueueDeclareAsync(retryQueue, durable: true, exclusive: false, autoDelete: false,
                arguments: new Dictionary<string, object?>
                {
                    ["x-message-ttl"] = _options.RetryDelayMilliseconds,
                    ["x-dead-letter-exchange"] = MessagingTopology.DefaultExchange,
                    ["x-dead-letter-routing-key"] = QueueName
                },
                cancellationToken: cancellationToken);

            // Ana kuyrukta reddedilen (nack, requeue: false) mesaj retry kuyruğuna gider.
            await channel.QueueDeclareAsync(QueueName, durable: true, exclusive: false, autoDelete: false,
                arguments: new Dictionary<string, object?>
                {
                    ["x-dead-letter-exchange"] = MessagingTopology.DefaultExchange,
                    ["x-dead-letter-routing-key"] = retryQueue
                },
                cancellationToken: cancellationToken);

            queue = QueueName;
        }
        else
        {
            var declared = await channel.QueueDeclareAsync(string.Empty, durable: false, exclusive: true, autoDelete: true, cancellationToken: cancellationToken);
            queue = declared.QueueName;
        }

        foreach (var routingKey in RoutingKeys)
            await channel.QueueBindAsync(queue, MessagingTopology.EventsExchange, routingKey, cancellationToken: cancellationToken);

        return queue;
    }

    private async Task OnReceivedAsync(IChannel channel, string queue, BasicDeliverEventArgs args, CancellationToken stoppingToken)
    {
        var message = new ReceivedMessage(args.RoutingKey, args.BasicProperties.MessageId, Encoding.UTF8.GetString(args.Body.Span));
        ConsumeResult result;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            result = await HandleAsync(message, scope.ServiceProvider, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Kapanış: mesaj deneme sayılmadan kuyruğa geri bırakılır.
            await channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: true, CancellationToken.None);
            return;
        }
        catch (Exception ex)
        {
            Logger.LogWarning(ex, "{Consumer} mesajı işleyemedi: {RoutingKey} ({MessageId})", GetType().Name, message.RoutingKey, message.MessageId);
            result = ConsumeResult.Retry;
        }

        try
        {
            await SettleAsync(channel, queue, args, result, stoppingToken);
        }
        catch (Exception ex)
        {
            // Onaylanamayan mesaj, kanal yeniden kurulduğunda broker tarafından tekrar teslim edilir.
            Logger.LogError(ex, "{Consumer} mesajı sonuçlandıramadı ({MessageId}).", GetType().Name, message.MessageId);
        }
    }

    private async Task SettleAsync(IChannel channel, string queue, BasicDeliverEventArgs args, ConsumeResult result, CancellationToken cancellationToken)
    {
        switch (result)
        {
            case ConsumeResult.Ack:
                await channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken);
                return;

            case ConsumeResult.Retry when !IsDurable:
                Logger.LogWarning("{Consumer} geçici kuyrukta başarısız mesajı atlıyor ({MessageId}).", GetType().Name, args.BasicProperties.MessageId);
                await channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken);
                return;

            case ConsumeResult.Retry:
                var previousAttempts = DeathHeader.GetRejectionCount(args.BasicProperties.Headers, queue);

                if (previousAttempts >= _options.MaxRetryAttempts)
                {
                    await MoveToDeadLetterQueueAsync(channel, args, $"{previousAttempts + 1} denemeden sonra başarısız", cancellationToken);
                    return;
                }

                Logger.LogInformation(
                    "{Consumer} mesajı yeniden denenecek ({Attempt}/{MaxAttempts}).",
                    GetType().Name, previousAttempts + 1, _options.MaxRetryAttempts);

                await channel.BasicNackAsync(args.DeliveryTag, multiple: false, requeue: false, cancellationToken);
                return;

            case ConsumeResult.DeadLetter:
                await MoveToDeadLetterQueueAsync(channel, args, "kalıcı hata", cancellationToken);
                return;
        }
    }

    private async Task MoveToDeadLetterQueueAsync(IChannel channel, BasicDeliverEventArgs args, string reason, CancellationToken cancellationToken)
    {
        if (!IsDurable)
        {
            await channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken);
            return;
        }

        var headers = args.BasicProperties.Headers is null
            ? new Dictionary<string, object?>()
            : new Dictionary<string, object?>(args.BasicProperties.Headers);

        headers[DeadLetterReasonHeader] = reason;

        var properties = new BasicProperties
        {
            Persistent = true,
            MessageId = args.BasicProperties.MessageId,
            Type = args.BasicProperties.Type,
            ContentType = args.BasicProperties.ContentType,
            Headers = headers
        };

        // Önce DLQ'ya (onaylı) yazılır, sonra orijinal onaylanır: arada çökme olursa mesaj kaybolmaz, en fazla tekrarlanır.
        await channel.BasicPublishAsync(
            MessagingTopology.DefaultExchange,
            MessagingTopology.DeadLetterQueueFor(QueueName),
            mandatory: false,
            properties,
            args.Body,
            cancellationToken);

        await channel.BasicAckAsync(args.DeliveryTag, multiple: false, cancellationToken);

        Logger.LogError(
            "{Consumer} mesajı ölü mektup kuyruğuna taşıdı ({Reason}): {RoutingKey} ({MessageId})",
            GetType().Name, reason, args.RoutingKey, args.BasicProperties.MessageId);
    }

    private static async Task DisposeQuietlyAsync(IChannel channel)
    {
        try
        {
            await channel.DisposeAsync();
        }
        catch
        {
            // Kapanmakta olan kanalın serbest bırakılmasındaki hata, yeniden bağlanmayı etkilemez.
        }
    }
}
