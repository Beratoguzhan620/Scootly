using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Scootly.Infrastructure.Messaging.Outbox;

public sealed class OutboxPublisherService : BackgroundService
{
    private static readonly TimeSpan MaxBackoff = TimeSpan.FromSeconds(60);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly MessagingOptions _options;
    private readonly ILogger<OutboxPublisherService> _logger;

    public OutboxPublisherService(
        IServiceScopeFactory scopeFactory,
        IOptions<MessagingOptions> options,
        ILogger<OutboxPublisherService> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var pollInterval = TimeSpan.FromMilliseconds(_options.OutboxPollIntervalMilliseconds);
        var backoff = pollInterval;

        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = pollInterval;

            try
            {
                using var scope = _scopeFactory.CreateScope();
                var processor = scope.ServiceProvider.GetRequiredService<OutboxProcessor>();

                var published = await processor.PublishPendingAsync(stoppingToken);

                // Parti doluysa bekleyen mesaj kalmış olabilir: beklemeden devam et.
                if (published >= _options.OutboxBatchSize)
                    delay = TimeSpan.Zero;

                backoff = pollInterval;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (OutboxPublishException ex)
            {
                // Broker kesintisinde her tur aynı mesajda durur; bekleme süresi büyüyerek broker'ı yormaz.
                _logger.LogWarning(ex.InnerException, "Outbox mesajı yayınlanamadı ({MessageId}); {Delay} sonra tekrar denenecek.", ex.MessageId, backoff);
                delay = backoff;
                backoff = TimeSpan.FromMilliseconds(Math.Min(backoff.TotalMilliseconds * 2, MaxBackoff.TotalMilliseconds));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox yayın turu başarısız; {Delay} sonra tekrar denenecek.", backoff);
                delay = backoff;
                backoff = TimeSpan.FromMilliseconds(Math.Min(backoff.TotalMilliseconds * 2, MaxBackoff.TotalMilliseconds));
            }

            if (delay > TimeSpan.Zero)
            {
                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
