namespace Scootly.Infrastructure.Messaging.Outbox;

/// <summary>
/// Kuyruğa gönderilmeyi bekleyen bir olay (66. gün).
/// </summary>
/// <remarks>
/// <see cref="Id"/> olayın kendi kimliği (<c>EventId</c>). Gönderici mesajı
/// bu kimlikle yayınlıyor; tüketici tarafındaki tekrar kontrolü (68. gün) de
/// bu kimliğe bakıyor. Yani aynı outbox kaydı iki kez gönderilse bile — ki
/// gönderici "gönderdim" diye işaretleyemeden çökerse olur — ikinci kopya
/// tüketicide tanınıp atlanıyor.
/// </remarks>
public sealed class OutboxMessage
{
    public const int MaxErrorLength = 1000;

    private OutboxMessage()
    {
        EventType = string.Empty;
        Payload = string.Empty;
    }

    public OutboxMessage(Guid id, string eventType, string payload, DateTime createdAt)
    {
        Id = id;
        EventType = eventType;
        Payload = payload;
        CreatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    /// <summary>Yönlendirme anahtarı — örnek: <c>ride.completed</c>.</summary>
    public string EventType { get; private set; }

    /// <summary>Olayın JSON hali; kuyruğa bayt bayt aynen gidiyor.</summary>
    public string Payload { get; private set; }

    public DateTime CreatedAt { get; private set; }

    /// <summary>Kuyruğa gönderilip RabbitMQ'nun onayladığı an; NULL ise bekliyor.</summary>
    public DateTime? ProcessedAt { get; private set; }

    /// <summary>Başarısız gönderim denemesi sayısı.</summary>
    public int Attempts { get; private set; }

    public string? LastError { get; private set; }

    public void MarkProcessed(DateTime processedAt)
    {
        ProcessedAt = processedAt;
        LastError = null;
    }

    public void RecordFailure(string error)
    {
        Attempts++;
        LastError = error.Length <= MaxErrorLength ? error : error[..MaxErrorLength];
    }
}
