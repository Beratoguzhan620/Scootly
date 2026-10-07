namespace Scootly.Infrastructure.Messaging.Outbox;

/// <summary>Partideki bir mesaj yayınlanamadı; ondan önceki mesajlar yayınlanıp kaydedildi.</summary>
public sealed class OutboxPublishException : Exception
{
    public Guid MessageId { get; }

    public int PublishedBeforeFailure { get; }

    public OutboxPublishException(Guid messageId, int publishedBeforeFailure, Exception innerException)
        : base($"Outbox mesajı yayınlanamadı ({messageId}); bu turda önce {publishedBeforeFailure} mesaj yayınlandı.", innerException)
    {
        MessageId = messageId;
        PublishedBeforeFailure = publishedBeforeFailure;
    }
}
