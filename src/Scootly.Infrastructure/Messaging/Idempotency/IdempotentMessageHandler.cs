using Microsoft.EntityFrameworkCore;
using Scootly.Infrastructure.Persistence;

namespace Scootly.Infrastructure.Messaging.Idempotency;

public sealed class IdempotentMessageHandler
{
    private readonly ScootlyDbContext _dbContext;

    public IdempotentMessageHandler(ScootlyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> TryProcessAsync(Guid messageId, Func<Task> action, CancellationToken cancellationToken = default)
    {
        var alreadyProcessed = await _dbContext.Set<ProcessedMessage>()
            .AnyAsync(m => m.MessageId == messageId, cancellationToken);

        if (alreadyProcessed)
            return false;

        await action();

        _dbContext.Set<ProcessedMessage>().Add(new ProcessedMessage(messageId, DateTime.UtcNow));
        await _dbContext.SaveChangesAsync(cancellationToken);

        return true;
    }
}