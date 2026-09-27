using Microsoft.EntityFrameworkCore;
using Scootly.Application.Abstractions;
using Scootly.Application.Abstractions.Exceptions;
using Scootly.Infrastructure.Persistence;

namespace Scootly.Infrastructure.Messaging.Idempotency;

/// <summary>
/// "Bu mesajı daha önce işledim mi?" kontrolü. Çağrılan işlemin kendisi de idempotent olmalıdır
/// (derinlemesine savunma): işlem ile kaydın yazılması arasında süreç çökerse mesaj yeniden işlenir.
/// </summary>
public sealed class IdempotentMessageHandler
{
    private readonly ScootlyDbContext _dbContext;
    private readonly IClock _clock;

    public IdempotentMessageHandler(ScootlyDbContext dbContext, IClock clock)
    {
        _dbContext = dbContext;
        _clock = clock;
    }

    /// <param name="action">Mesajı işler; mesajın "işlendi" olarak kaydedilmesi gerekiyorsa true döner
    /// (geçici bir hata nedeniyle tekrar denenmesi gerekiyorsa false).</param>
    /// <returns>İşlem bu çağrıda çalıştırıldıysa true, mesaj daha önce işlenmişse false.</returns>
    public async Task<bool> TryProcessAsync(
        Guid messageId,
        string consumer,
        Func<CancellationToken, Task<bool>> action,
        CancellationToken cancellationToken = default)
    {
        var alreadyProcessed = await _dbContext.ProcessedMessages
            .AsNoTracking()
            .AnyAsync(m => m.MessageId == messageId && m.Consumer == consumer, cancellationToken);

        if (alreadyProcessed)
            return false;

        if (!await action(cancellationToken))
            return true;

        _dbContext.ProcessedMessages.Add(new ProcessedMessage(messageId, consumer, _clock.UtcNow));

        try
        {
            await _dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException)
        {
            // Aynı mesaj eşzamanlı olarak başka bir yerde de işlendi; sonuç idempotent olduğu için sorun değil.
            _dbContext.DiscardChanges();
            return false;
        }

        return true;
    }
}
