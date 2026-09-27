using RabbitMQ.Client;
using Scootly.Application.IntegrationEvents;

namespace Scootly.Infrastructure.Messaging;

/// <summary>Bir tüketicinin kuyruğu ve dinlediği olay.</summary>
public sealed record QueueDefinition(string Name, string RoutingKey);

/// <summary>
/// Exchange'ler, kuyruklar ve bağlamalar — hepsi tek yerde (62-65. günler).
/// </summary>
/// <remarks>
/// <para>
/// <b>Mesajın yolu:</b> yayınlayıcı olayı <see cref="EventsExchange"/>'e
/// olay adıyla (örnek: <c>ride.completed</c>) gönderir; o ada bağlı her kuyruk
/// bir kopya alır. Tüketici başarısız olursa mesaj <see cref="RetryExchange"/>
/// üzerinden <c>.retry</c> kuyruğuna gider, orada
/// <see cref="DeliveryPolicy.RetryDelay"/> kadar bekler, süresi dolunca
/// RabbitMQ onu kendiliğinden ana kuyruğa geri koyar. Deneme hakkı biterse ya
/// da mesaj zehirliyse <see cref="DeadLetterExchange"/> üzerinden <c>.dlq</c>
/// kuyruğuna gider ve orada bir insanın bakmasını bekler.
/// </para>
/// <para>
/// <b>Yayınlayıcı da tüketici de BÜTÜN topolojiyi bildiriyor.</b> RabbitMQ'da
/// bir exchange'e gelen mesaj, bağlı kuyruk yoksa sessizce atılır. Kuyrukları
/// yalnızca tüketici bildirseydi, Worker hiç başlatılmamışken API'nin
/// yayınladığı her olay kaybolurdu — ve bunu hiçbir log söylemezdi. Aynı
/// bildirimi iki kez yapmak zararsız (bildirimler idempotent), bir kez bile
/// yapmamak ise veri kaybı.
/// </para>
/// <para>
/// Bir kuyruğun argümanları sonradan DEĞİŞTİRİLEMEZ; farklı argümanla tekrar
/// bildirmek <c>PRECONDITION_FAILED</c> ile kanalı kapatır. Bekleme süresini
/// değiştirirsen ilgili <c>.retry</c> kuyruğunu yönetim arayüzünden silmen
/// gerekir.
/// </para>
/// </remarks>
public static class RabbitMqTopology
{
    /// <summary>Olayların ilk düştüğü yer. Topic: yönlendirme anahtarı olay adı.</summary>
    public const string EventsExchange = "scootly.events";

    /// <summary>Yeniden denenecek mesajlar. Direct: anahtar hedef kuyruğun adı.</summary>
    public const string RetryExchange = "scootly.retry";

    /// <summary>Artık denenmeyecek mesajlar. Direct: anahtar hedef kuyruğun adı.</summary>
    public const string DeadLetterExchange = "scootly.dead";

    /// <summary>Sürüş bitti → ücret hesabı.</summary>
    public static readonly QueueDefinition FareCalculation =
        new("scootly.fare-calculation", RideCompletedIntegrationEvent.EventName);

    /// <summary>Batarya düştü → saha görevi.</summary>
    public static readonly QueueDefinition FieldTasks =
        new("scootly.field-tasks", VehicleBatteryLowIntegrationEvent.EventName);

    public static IReadOnlyList<QueueDefinition> Queues { get; } = [FareCalculation, FieldTasks];

    public static string RetryQueueOf(string queueName) => queueName + ".retry";

    public static string DeadLetterQueueOf(string queueName) => queueName + ".dlq";

    public static async Task DeclareAsync(IChannel channel, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(channel);

        await channel.ExchangeDeclareAsync(EventsExchange, ExchangeType.Topic, durable: true, autoDelete: false, cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(RetryExchange, ExchangeType.Direct, durable: true, autoDelete: false, cancellationToken: cancellationToken);
        await channel.ExchangeDeclareAsync(DeadLetterExchange, ExchangeType.Direct, durable: true, autoDelete: false, cancellationToken: cancellationToken);

        foreach (var kuyruk in Queues)
        {
            // Ana kuyruk: dayanikli, RabbitMQ yeniden baslasa da kaybolmaz.
            await channel.QueueDeclareAsync(kuyruk.Name, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: cancellationToken);
            await channel.QueueBindAsync(kuyruk.Name, EventsExchange, kuyruk.RoutingKey, cancellationToken: cancellationToken);

            // Bekleme kuyrugu: kimse dinlemiyor. Mesaj TTL kadar bekler, suresi
            // dolunca varsayilan exchange ("") uzerinden adi gecen ana kuyruga
            // geri doner. Ana kuyrugun basina hemen geri koymak (requeue)
            // mesaji milisaniyeler icinde tekrar getirirdi; gecici bir kesinti
            // o surede gecmez ve uc deneme bir saniyede tukenirdi.
            var beklemeArgumanlari = new Dictionary<string, object?>
            {
                ["x-message-ttl"] = (int)DeliveryPolicy.RetryDelay.TotalMilliseconds,
                ["x-dead-letter-exchange"] = string.Empty,
                ["x-dead-letter-routing-key"] = kuyruk.Name
            };

            var bekleme = RetryQueueOf(kuyruk.Name);
            await channel.QueueDeclareAsync(bekleme, durable: true, exclusive: false, autoDelete: false, arguments: beklemeArgumanlari, cancellationToken: cancellationToken);
            await channel.QueueBindAsync(bekleme, RetryExchange, kuyruk.Name, cancellationToken: cancellationToken);

            // Olu mektup kuyrugu: kimse dinlemiyor, bilerek. Buradaki mesajlar
            // bir insanin bakmasini bekliyor.
            var olu = DeadLetterQueueOf(kuyruk.Name);
            await channel.QueueDeclareAsync(olu, durable: true, exclusive: false, autoDelete: false, arguments: null, cancellationToken: cancellationToken);
            await channel.QueueBindAsync(olu, DeadLetterExchange, kuyruk.Name, cancellationToken: cancellationToken);
        }
    }
}
