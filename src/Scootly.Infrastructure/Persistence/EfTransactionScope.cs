using Microsoft.EntityFrameworkCore.Storage;
using Scootly.Application.Abstractions;

namespace Scootly.Infrastructure.Persistence;

/// <summary>
/// <see cref="ITransactionScope"/>'un EF Core karşılığı.
/// Application katmanının EF tiplerini görmemesi için araya giren ince sarmalayıcı.
/// </summary>
internal sealed class EfTransactionScope : ITransactionScope
{
    private readonly IDbContextTransaction _transaction;

    public EfTransactionScope(IDbContextTransaction transaction)
    {
        _transaction = transaction;
    }

    public Task CommitAsync(CancellationToken cancellationToken = default) =>
        _transaction.CommitAsync(cancellationToken);

    public Task RollbackAsync(CancellationToken cancellationToken = default) =>
        _transaction.RollbackAsync(cancellationToken);

    public ValueTask DisposeAsync() => _transaction.DisposeAsync();
}
