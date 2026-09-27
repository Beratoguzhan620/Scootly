namespace Scootly.Infrastructure.Messaging.Idempotency;

/// <summary>
/// Bir tüketicinin işlediği mesajın kaydı. Anahtar (MessageId, Consumer) çiftidir:
/// aynı mesaj farklı tüketiciler tarafından bağımsız olarak işlenebilir.
/// </summary>
public sealed class ProcessedMessage
{
    public const int ConsumerMaxLength = 200;

    public Guid MessageId { get; private set; }
    public string Consumer { get; private set; } = null!;
    public DateTime ProcessedAt { get; private set; }

    private ProcessedMessage() { }

    public ProcessedMessage(Guid messageId, string consumer, DateTime processedAt)
    {
        MessageId = messageId;
        Consumer = consumer;
        ProcessedAt = processedAt;
    }
}
