using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Scootly.Application.Abstractions;
using Scootly.Application.IntegrationEvents;

namespace Scootly.Infrastructure.Messaging;

/// <summary>
/// Entegrasyon olaylarını RabbitMQ'ya gönderir (62. gün).
/// </summary>
/// <remarks>
/// <para>
/// Mesajlar <b>kalıcı</b> (persistent) gönderiliyor ve kuyruklar dayanıklı:
/// RabbitMQ yeniden başlasa bile kuyrukta bekleyen olaylar kaybolmuyor.
/// İkisinden biri eksik olsaydı — dayanıklı kuyrukta kalıcı olmayan mesaj ya
/// da tersi — yeniden başlatmada kuyruk dururdu ama içi boşalırdı.
/// </para>
/// <para>
/// <b>Tek kanal, kilitle.</b> Bir kanal aynı anda iki iş parçacığından
/// yayınlamaya uygun değil; iki eşzamanlı sürüş bitirme isteği aynı kanalda
/// çakışır ve kanal hata verip kapanırdı. Her yayınlamada yeni kanal açmak da
/// çözüm olurdu ama her seferinde bir ağ gidiş-dönüşü demek.
/// </para>
/// <para>
/// <b>Yayıncı onayı (publisher confirm) YOK.</b> Metot döndüğünde mesaj ağa
/// yazılmış ama RabbitMQ'nun onu diske aldığı garanti değil. 67. günün outbox
/// göndericisi "gönderildi" işaretini ancak bu onaydan sonra koymalı; o gün
/// eklenecek.
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

        // Anlamsiz bir olayi kuyruga koymak, sorunu tuketiciye ve olu mektup
        // kuyruguna tasimak demek. Yayinlayan tarafta yakalamak daha ucuz.
        integrationEvent.Validate();

        var govde = IntegrationEventSerializer.Serialize(integrationEvent);

        var ozellikler = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            MessageId = integrationEvent.EventId.ToString(),
            Type = TEvent.EventName,
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
                TEvent.EventName,
                false,
                ozellikler,
                govde,
                cancellationToken);
        }
        finally
        {
            _kilit.Release();
        }

        _logger.LogInformation(
            "Olay yayinlandi: {EventName} EventId={EventId}", TEvent.EventName, integrationEvent.EventId);
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
        var kanal = await baglanti.CreateChannelAsync(cancellationToken: cancellationToken);

        await RabbitMqTopology.DeclareAsync(kanal, cancellationToken);

        _kanal = kanal;
        return kanal;
    }
}
