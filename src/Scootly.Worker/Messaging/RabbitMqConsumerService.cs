using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Scootly.Application.IntegrationEvents;
using Scootly.Infrastructure.Messaging;
using Scootly.Infrastructure.Messaging.Consumers;

namespace Scootly.Worker.Messaging;

/// <summary>
/// Bir kuyruğu dinler, her mesajı ilgili tüketiciye verir ve sonucuna göre
/// onaylar, yeniden dener ya da ölü mektup kuyruğuna taşır (64-65. günler).
/// </summary>
/// <remarks>
/// <para>
/// <b>Onay (ack) işlemden SONRA.</b> Otomatik onay (<c>autoAck: true</c>)
/// mesajı teslim edildiği anda kuyruktan siler; tüketici tam o sırada çökerse
/// mesaj kaybolur. Elle onayda çöken tüketicinin mesajı kuyruğa geri döner ve
/// bir sonrakine verilir — bedeli, aynı mesajın iki kez işlenebilmesi. Bu
/// yüzden tüketicilerin tekrar gelen mesaja dayanıklı olması şart (68. gün).
/// </para>
/// <para>
/// <b>Üç ayrı başarısızlık, üç ayrı yol</b> (<see cref="DeliveryPolicy"/>):
/// okunamayan mesaj (zehirli) ve iş kuralının reddettiği mesaj hiç yeniden
/// denenmeden ölü mektup kuyruğuna gider; istisna fırlatan mesaj
/// <see cref="DeliveryPolicy.RetryDelay"/> sonra tekrar gelir, toplam
/// <see cref="DeliveryPolicy.MaxAttempts"/> denemeden sonra o da ölü kuyruğa
/// düşer. Hiçbiri ana kuyruğu tıkamıyor.
/// </para>
/// <para>
/// <b>RabbitMQ kapalıysa servis ÇÖKMÜYOR</b>, birkaç saniyede bir yeniden
/// bağlanmayı deniyor. .NET 8'den beri bir arka plan servisinden kaçan istisna
/// bütün Worker sürecini durduruyor; kuyruk kesintisi yüzünden rezervasyon
/// zaman aşımı servisinin de durması kabul edilemezdi.
/// </para>
/// </remarks>
public sealed class RabbitMqConsumerService<TEvent, TConsumer> : BackgroundService
    where TEvent : IIntegrationEvent, IHasEventName
    where TConsumer : class, IIntegrationEventConsumer<TEvent>
{
    private static readonly TimeSpan YenidenBaglanmaBeklemesi = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan KanalKontrolAraligi = TimeSpan.FromSeconds(2);
    private const int SebepUzunlugu = 500;

    private readonly RabbitMqConnectionProvider _connectionProvider;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly RabbitMqOptions _options;
    private readonly ILogger<RabbitMqConsumerService<TEvent, TConsumer>> _logger;

    public RabbitMqConsumerService(
        RabbitMqConnectionProvider connectionProvider,
        IServiceScopeFactory scopeFactory,
        RabbitMqOptions options,
        ILogger<RabbitMqConsumerService<TEvent, TConsumer>> logger)
    {
        _connectionProvider = connectionProvider;
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var kuyruk = TConsumer.Queue;

        // Yapilandirma hatasi: tuketici baska bir olayin kuyrugunu dinliyor.
        // Bu calisma zamaninda sessizce "hicbir mesaj islenmiyor" olarak
        // gorunurdu; aciliyor ve Worker'i durduruyoruz.
        if (!string.Equals(kuyruk.RoutingKey, TEvent.EventName, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"{typeof(TConsumer).Name} '{kuyruk.Name}' kuyrugunu dinliyor ama o kuyruk " +
                $"'{kuyruk.RoutingKey}' olaylarina bagli, '{TEvent.EventName}' degil.");
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            IChannel? kanal = null;

            try
            {
                var baglanti = await _connectionProvider.GetConnectionAsync(stoppingToken);
                kanal = await baglanti.CreateChannelAsync(cancellationToken: stoppingToken);

                await RabbitMqTopology.DeclareAsync(kanal, stoppingToken);
                await kanal.BasicQosAsync(0, _options.PrefetchCount, false, stoppingToken);

                var tuketici = new AsyncEventingBasicConsumer(kanal);
                var dinlenenKanal = kanal;
                tuketici.ReceivedAsync += (_, teslim) => TeslimiIsleAsync(dinlenenKanal, teslim, kuyruk, stoppingToken);

                await kanal.BasicConsumeAsync(kuyruk.Name, false, tuketici, stoppingToken);

                _logger.LogInformation(
                    "{Tuketici} '{Kuyruk}' kuyrugunu dinliyor.", typeof(TConsumer).Name, kuyruk.Name);

                while (kanal.IsOpen && !stoppingToken.IsCancellationRequested)
                {
                    await Task.Delay(KanalKontrolAraligi, stoppingToken);
                }

                if (!stoppingToken.IsCancellationRequested)
                {
                    _logger.LogWarning("'{Kuyruk}' kanali kapandi, yeniden baglaniliyor.", kuyruk.Name);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex,
                    "'{Kuyruk}' dinlenemiyor. {Saniye} sn sonra yeniden denenecek.",
                    kuyruk.Name, YenidenBaglanmaBeklemesi.TotalSeconds);
            }
            finally
            {
                if (kanal is not null)
                {
                    try
                    {
                        await kanal.DisposeAsync();
                    }
                    catch (Exception)
                    {
                        // kopmus kanal; yenisi acilacak
                    }
                }
            }

            try
            {
                await Task.Delay(YenidenBaglanmaBeklemesi, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task TeslimiIsleAsync(
        IChannel kanal,
        BasicDeliverEventArgs teslim,
        QueueDefinition kuyruk,
        CancellationToken stoppingToken)
    {
        // Govde yalnizca bu metot suresince gecerli; kutuphane tamponu yeniden
        // kullaniyor. Yeniden yayinlarken de lazim oldugu icin kopyalaniyor.
        var govde = teslim.Body.ToArray();
        var deneme = MessageHeaders.ReadAttempt(teslim.BasicProperties.Headers);
        var mesajKimligi = teslim.BasicProperties.MessageId ?? "(yok)";

        try
        {
            TEvent olay;

            try
            {
                olay = IntegrationEventSerializer.Deserialize<TEvent>(govde);
                olay.Validate();
            }
            catch (Exception ex) when (ex is JsonException or InvalidIntegrationEventException or NotSupportedException)
            {
                _logger.LogError(ex,
                    "Zehirli mesaj: '{Kuyruk}' MessageId={MessageId}. Yeniden denenmeyecek.",
                    kuyruk.Name, mesajKimligi);

                await SonuclandirAsync(kanal, teslim, govde, kuyruk, FailureKind.Poison, deneme, ex.Message, stoppingToken);
                return;
            }

            Scootly.Domain.Common.Result sonuc;

            try
            {
                await using var kapsam = _scopeFactory.CreateAsyncScope();
                var tuketici = kapsam.ServiceProvider.GetRequiredService<TConsumer>();

                sonuc = await tuketici.ConsumeAsync(olay, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Kapanis sirasinda yarida kaldi. Onay YOK: baglanti kapaninca
                // RabbitMQ mesaji kuyruga geri koyar ve bir dahaki acilista
                // yeniden islenir.
                return;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex,
                    "'{Kuyruk}' MessageId={MessageId} islenemedi (deneme {Deneme}/{Azami}).",
                    kuyruk.Name, mesajKimligi, deneme, DeliveryPolicy.MaxAttempts);

                await SonuclandirAsync(kanal, teslim, govde, kuyruk, FailureKind.Transient, deneme,
                    $"{ex.GetType().Name}: {ex.Message}", stoppingToken);
                return;
            }

            if (!sonuc.IsSuccess)
            {
                _logger.LogWarning(
                    "'{Kuyruk}' MessageId={MessageId} reddedildi: {Sebep}", kuyruk.Name, mesajKimligi, sonuc.Error);

                await SonuclandirAsync(kanal, teslim, govde, kuyruk, FailureKind.Rejected, deneme, sonuc.Error, stoppingToken);
                return;
            }

            await kanal.BasicAckAsync(teslim.DeliveryTag, false, stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // kapanis; onaylanmayan mesaj kuyruga geri doner
        }
        catch (Exception ex)
        {
            // Buraya dusmek onay/yayin katmaninda bir sorun demek (kanal koptu).
            // Onaylanmamis mesaj baglanti kapaninca kuyruga geri doner.
            _logger.LogError(ex, "'{Kuyruk}' MessageId={MessageId} sonuclandirilamadi.", kuyruk.Name, mesajKimligi);
        }
    }

    private async Task SonuclandirAsync(
        IChannel kanal,
        BasicDeliverEventArgs teslim,
        byte[] govde,
        QueueDefinition kuyruk,
        FailureKind tur,
        int deneme,
        string sebep,
        CancellationToken stoppingToken)
    {
        var karar = DeliveryPolicy.OnFailure(tur, deneme);

        var (exchange, sonrakiDeneme) = karar == DeliveryOutcome.Retry
            ? (RabbitMqTopology.RetryExchange, deneme + 1)
            : (RabbitMqTopology.DeadLetterExchange, deneme);

        var ozellikler = new BasicProperties
        {
            Persistent = true,
            ContentType = teslim.BasicProperties.ContentType ?? "application/json",
            MessageId = teslim.BasicProperties.MessageId,
            Type = teslim.BasicProperties.Type,
            Headers = new Dictionary<string, object?>
            {
                [MessageHeaders.Attempt] = sonrakiDeneme,
                [MessageHeaders.FailureKind] = tur.ToString(),
                [MessageHeaders.FailureReason] = sebep.Length <= SebepUzunlugu ? sebep : sebep[..SebepUzunlugu],
                [MessageHeaders.FailedQueue] = kuyruk.Name
            }
        };

        try
        {
            // SIRA: once yeni yere yaz, sonra eskisini onayla. Tersi olsaydi
            // ikisinin arasinda cokmek mesaji iki yerden de silerdi. Bu sirayla
            // en kotu durum ayni mesajin iki kopyasi — ki en az bir kez teslimde
            // zaten beklenen bir durum.
            await kanal.BasicPublishAsync(exchange, kuyruk.Name, false, ozellikler, govde, stoppingToken);
            await kanal.BasicAckAsync(teslim.DeliveryTag, false, stoppingToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex,
                "'{Kuyruk}' mesaji {Hedef} tasinamadi; ana kuyruga geri birakiliyor.", kuyruk.Name, exchange);

            await kanal.BasicNackAsync(teslim.DeliveryTag, false, true, stoppingToken);
            return;
        }

        if (karar == DeliveryOutcome.Retry)
        {
            _logger.LogWarning(
                "'{Kuyruk}' mesaji {Saniye} sn sonra yeniden denenecek (deneme {Sonraki}/{Azami}).",
                kuyruk.Name, DeliveryPolicy.RetryDelay.TotalSeconds, sonrakiDeneme, DeliveryPolicy.MaxAttempts);
        }
        else
        {
            _logger.LogError(
                "'{Kuyruk}' mesaji OLU MEKTUP kuyruguna tasindi: {Dlq}. Tur={Tur}, deneme={Deneme}, sebep: {Sebep}",
                kuyruk.Name, RabbitMqTopology.DeadLetterQueueOf(kuyruk.Name), tur, deneme, sebep);
        }
    }
}
