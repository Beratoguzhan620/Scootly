using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Scootly.Application.Abstractions;
using Scootly.Application.IntegrationEvents;

namespace Scootly.Infrastructure.Messaging;

/// <summary>
/// Olayları RabbitMQ'ya gönderir (62. gün; 67. günde yayıncı onayı eklendi).
/// </summary>
/// <remarks>
/// <para>
/// Mesajlar <b>kalıcı</b> (persistent) gönderiliyor ve kuyruklar dayanıklı:
/// RabbitMQ yeniden başlasa bile kuyrukta bekleyen olaylar kaybolmuyor.
/// </para>
/// <para>
/// <b>Yayıncı onayı (publisher confirm) AÇIK — 67. gün.</b> Metot ancak
/// RabbitMQ mesajı aldığını onayladıktan sonra dönüyor; reddederse
/// (<c>nack</c>) ya da mesaj hiçbir kuyruğa yönlenemezse istisna fırlatıyor.
/// Outbox göndericisi "gönderildi" işaretini bu dönüşe bağlıyor. Onay
/// olmasaydı metot mesaj ağa yazılır yazılmaz dönerdi ve arada kopan bir
/// bağlantı olayı, kimse fark etmeden kaybettirirdi.
/// </para>
/// <para>
/// <b>mandatory: true.</b> Bağlı kuyruğu olmayan bir olay RabbitMQ'da
/// sessizce atılır. Bu bayrakla atılmak yerine geri dönüyor ve yayınlama
/// başarısız sayılıyor — outbox kaydı "gönderilmedi" olarak kalıyor.
/// </para>
/// <para>
/// Tek kanal, kilitle: bir kanal eşzamanlı yayınlamaya uygun değil.
/// </para>
/// </remarks>
public sealed class RabbitMqEventPublisher : IEventPublisher, IAsyncDisposable
{
    private readonly RabbitMqConnectionProvider _connectionProvider;
    private readonly ILogger<RabbitMqEventPublisher> _logger;
    private readonly SemaphoreSlim _kilit = new(1, 1);
    private IChannel? _kanal;

    public RabbitMqEventPublisher(
        RabbitMqConnectionProvider connectionProvider,
        ILogger<RabbitMqEventPublisher> logger)
    {
        _connectionProvider = connectionProvider;
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent, IHasEventName
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        integrationEvent.Validate();

        await PublishRawAsync(
            TEvent.EventName,
            integrationEvent.EventId,
            IntegrationEventSerializer.Serialize(integrationEvent),
            cancellationToken);
    }

    /// <summary>
    /// Hazır bir gövdeyi yayınlar ve RabbitMQ'nun onayını bekler (outbox göndericisi için).
    /// </summary>
    public async Task PublishRawAsync(string eventType, Guid messageId, byte[] body, CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentNullException.ThrowIfNull(body);

        var ozellikler = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            MessageId = messageId.ToString(),
            Type = eventType,
            Headers = new Dictionary<string, object?>
            {
                [MessageHeaders.Attempt] = 1
            }
        };

        await _kilit.WaitAsync(cancellationToken);
        try
        {
            var kanal = await KanalAsync(cancellationToken);

            await kanal.BasicPublishAsync(
                RabbitMqTopology.EventsExchange,
                eventType,
                true,
                ozellikler,
                body,
                cancellationToken);
        }
        finally
        {
            _kilit.Release();
        }

        _logger.LogInformation("Olay yayinlandi: {EventType} MessageId={MessageId}", eventType, messageId);
    }

    public async ValueTask DisposeAsync()
    {
        if (_kanal is not null)
        {
            try
            {
                await _kanal.DisposeAsync();
            }
            catch (Exception)
            {
                // kapanista; yapilacak bir sey yok
            }
        }

        _kilit.Dispose();
    }

    private async Task<IChannel> KanalAsync(CancellationToken cancellationToken)
    {
        if (_kanal is { IsOpen: true })
            return _kanal;

        if (_kanal is not null)
        {
            try
            {
                await _kanal.DisposeAsync();
            }
            catch (Exception)
            {
                // kopmus kanal; yenisi aciliyor
            }
        }

        var baglanti = await _connectionProvider.GetConnectionAsync(cancellationToken);

        // Onay izleme acik: BasicPublishAsync onaylanana kadar bekliyor,
        // nack ya da yonlendirilemeyen mesajda istisna firlatiyor.
        var kanal = await baglanti.CreateChannelAsync(
            new CreateChannelOptions(
                publisherConfirmationsEnabled: true,
                publisherConfirmationTrackingEnabled: true),
            cancellationToken);

        await RabbitMqTopology.DeclareAsync(kanal, cancellationToken);

        _kanal = kanal;
        return kanal;
    }
}
