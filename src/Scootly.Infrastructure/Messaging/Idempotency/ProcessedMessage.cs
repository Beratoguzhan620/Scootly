namespace Scootly.Infrastructure.Messaging.Idempotency;

/// <summary>Bir tüketicinin işlediği bir mesaj (68. gün).</summary>
/// <remarks>
/// Birincil anahtar (MessageId, Consumer). Aynı anda iki tüketici aynı mesajı
/// işlerse ikincisinin kaydı bu anahtar yüzünden reddedilir ve onunla aynı
/// transaction'daki iş de geri alınır — "etkisi bir kez"i sağlayan son hat bu.
/// </remarks>
public sealed class ProcessedMessage
{
    public const int MaxConsumerLength = 100;

    private ProcessedMessage()
    {
        Consumer = string.Empty;
    }

    public ProcessedMessage(Guid messageId, string consumer, DateTime processedAt)
    {
        MessageId = messageId;
        Consumer = consumer;
        ProcessedAt = processedAt;
    }

    public Guid MessageId { get; private set; }

    public string Consumer { get; private set; }

    public DateTime ProcessedAt { get; private set; }
}
