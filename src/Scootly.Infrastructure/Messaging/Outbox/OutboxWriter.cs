using System.Text;
using Scootly.Application.Abstractions;
using Scootly.Application.IntegrationEvents;
using Scootly.Infrastructure.Persistence;

namespace Scootly.Infrastructure.Messaging.Outbox;

/// <summary>Olayı outbox tablosuna ekler; kaydetmez (66. gün).</summary>
/// <remarks>
/// Kaydetmemesi bilinçli: kaydı, olayı üreten işleyicinin kendi
/// <c>SaveChangesAsync</c>'i yapıyor. Böylece olay ile olaya sebep olan
/// değişiklik tek bir transaction'da gidiyor. Burada kaydetseydik ikili yazma
/// problemi olduğu gibi geri gelirdi, yalnızca bu sefer iki tablo arasında.
/// </remarks>
public sealed class OutboxWriter : IOutboxWriter
{
    private readonly ScootlyDbContext _dbContext;

    public OutboxWriter(ScootlyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task WriteAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent, IHasEventName
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        // Yazarken dogrula: anlamsiz bir olayi outbox'a koymak, sorunu
        // saniyeler sonra baska bir surece (gonderici, tuketici) tasimak demek.
        integrationEvent.Validate();

        var govde = Encoding.UTF8.GetString(IntegrationEventSerializer.Serialize(integrationEvent));

        await _dbContext.OutboxMessages.AddAsync(
            new OutboxMessage(integrationEvent.EventId, TEvent.EventName, govde, integrationEvent.OccurredOnUtc),
            cancellationToken);
    }
}
