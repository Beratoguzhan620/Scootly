using Microsoft.EntityFrameworkCore;
using Scootly.Application.Abstractions;
using Scootly.Infrastructure.Persistence;

namespace Scootly.Infrastructure.Messaging.Idempotency;

public sealed class ProcessedMessageStore : IProcessedMessageStore
{
    private readonly ScootlyDbContext _dbContext;
    private readonly IClock _clock;

    public ProcessedMessageStore(ScootlyDbContext dbContext, IClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    public Task<bool> HasBeenProcessedAsync(Guid messageId, string consumer, CancellationToken cancellationToken = default)
    {
        return _dbContext.ProcessedMessages
            .AsNoTracking()
            .AnyAsync(m => m.MessageId == messageId && m.Consumer == consumer, cancellationToken);
    }

    public async Task MarkAsProcessedAsync(Guid messageId, string consumer, CancellationToken cancellationToken = default)
    {
        await _dbContext.ProcessedMessages.AddAsync(
            new ProcessedMessage(messageId, consumer, _clock.UtcNow), cancellationToken);
    }
}
