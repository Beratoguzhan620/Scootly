namespace Scootly.Infrastructure.Messaging.Idempotency;

public sealed class ProcessedMessage
{
    public Guid MessageId { get; private set; }
    public DateTime ProcessedAt { get; private set; }

    private ProcessedMessage() { }

    public ProcessedMessage(Guid messageId, DateTime processedAt)
    {
        MessageId = messageId;
        ProcessedAt = processedAt;
    }
}