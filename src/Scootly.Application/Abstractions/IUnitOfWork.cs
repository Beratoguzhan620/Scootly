namespace Scootly.Application.Abstractions;

public interface IUnitOfWork
{
    /// <summary>
    /// Değişiklikleri tek bir transaction içinde kaydeder.
    /// Eşzamanlılık çakışmasında <see cref="Exceptions.ConcurrencyConflictException"/>,
    /// benzersizlik ihlalinde <see cref="Exceptions.UniqueConstraintViolationException"/> fırlatır.
    /// </summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>Kaydedilmemiş tüm değişiklikleri ve izlenen varlıkları bırakır (yeniden deneme öncesi).</summary>
    void DiscardChanges();
}
