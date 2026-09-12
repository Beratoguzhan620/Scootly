namespace Scootly.Application.Abstractions;

/// <summary>
/// Bir veritabanı işlemi (transaction) sınırı.
/// </summary>
/// <remarks>
/// Application katmanı EF Core'un <c>IDbContextTransaction</c> tipini görmüyor;
/// gördüğü bu arayüz. Karar 1'in (Application somut teknolojiden bağımsız
/// olmalı) gereği: transaction bir iş kavramı, EF Core bir uygulama detayı.
/// </remarks>
public interface ITransactionScope : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);

    Task RollbackAsync(CancellationToken cancellationToken = default);
}
