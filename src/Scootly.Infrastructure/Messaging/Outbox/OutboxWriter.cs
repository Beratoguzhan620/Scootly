using System.Text.Json;
using Scootly.Application.Abstractions;
using Scootly.Infrastructure.Persistence;

namespace Scootly.Infrastructure.Messaging.Outbox;

public sealed class OutboxWriter : IOutboxWriter
{
    private readonly ScootlyDbContext _dbContext;

    public OutboxWriter(ScootlyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public void Write<T>(string eventType, T payload)
    {
        var serialized = JsonSerializer.Serialize(payload);

        var message = new OutboxMessage(
            Guid.NewGuid(),
            eventType,
            serialized,
            DateTime.UtcNow);

        _dbContext.Set<OutboxMessage>().Add(message);
    }
}