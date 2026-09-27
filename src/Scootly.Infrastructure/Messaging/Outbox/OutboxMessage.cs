namespace Scootly.Infrastructure.Messaging.Outbox;

public sealed class OutboxMessage
{
    public const int LastErrorMaxLength = 1000;

    public Guid Id { get; private set; }
    public string EventType { get; private set; } = null!;
    public string Payload { get; private set; } = null!;
    public DateTime CreatedAt { get; private set; }
    public DateTime? ProcessedAt { get; private set; }
    public int Attempts { get; private set; }
    public string? LastError { get; private set; }

    private OutboxMessage() { }

    public OutboxMessage(Guid id, string eventType, string payload, DateTime createdAt)
    {
        Id = id;
        EventType = eventType;
        Payload = payload;
        CreatedAt = createdAt;
    }

    public void MarkAsProcessed(DateTime processedAt)
    {
        Attempts++;
        ProcessedAt = processedAt;
        LastError = null;
    }

    public void RecordFailedAttempt(string error)
    {
        Attempts++;
        LastError = error.Length <= LastErrorMaxLength ? error : error[..LastErrorMaxLength];
    }
}
