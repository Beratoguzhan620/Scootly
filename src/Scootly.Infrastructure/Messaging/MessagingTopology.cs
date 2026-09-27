namespace Scootly.Infrastructure.Messaging;

/// <summary>
/// Kuyruk topolojisi. Her kalıcı tüketici kuyruğunun iki yardımcısı vardır:
/// <list type="bullet">
/// <item><c>{kuyruk}.retry</c>: reddedilen mesaj burada TTL kadar bekler, sonra ana kuyruğa geri döner.</item>
/// <item><c>{kuyruk}.dlq</c>: deneme hakkı biten veya kalıcı olarak bozuk mesajların park edildiği yer.</item>
/// </list>
/// </summary>
public static class MessagingTopology
{
    public const string EventsExchange = "scootly.events";

    /// <summary>RabbitMQ'nun varsayılan (adsız) exchange'i: yönlendirme anahtarı doğrudan kuyruk adıdır.</summary>
    public const string DefaultExchange = "";

    public static string RetryQueueFor(string queueName) => $"{queueName}.retry";

    public static string DeadLetterQueueFor(string queueName) => $"{queueName}.dlq";
}
