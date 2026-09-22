using Microsoft.EntityFrameworkCore;
using Scootly.Application.Abstractions;

namespace Scootly.Infrastructure.Persistence;

/// <summary>
/// <see cref="IQueryExecutor"/>'ın EF Core uygulaması.
/// </summary>
/// <remarks>
/// <para>
/// Projenin EF'e bağımlı olduğu yer burası olsun diye var. Üç satırlık bir
/// sınıf, ama taşıdığı sınır gerçek: <c>ToListAsync</c>, <c>CountAsync</c> ve
/// <c>FirstOrDefaultAsync</c> asenkron biçimleriyle LINQ'e değil EF Core'a ait.
/// Application katmanındaki sorgu handler'ları onları doğrudan çağırsaydı,
/// Application'ın paket listesine EF Core girerdi.
/// </para>
/// <para>
/// <b>Sessiz tuzak:</b> EF'in asenkron uzantıları yalnızca EF'in kendi sorgu
/// sağlayıcısıyla çalışır. Bellekteki bir listeye (<c>AsQueryable()</c>)
/// uygulanırsa çalışma zamanında <c>InvalidOperationException</c> fırlatır —
/// derleme temizdir, hata ancak test çalışırken görülür. Testlerin sahte bir
/// yürütücü kullanabilmesi, bu arayüzün ikinci kazancı.
/// </para>
/// </remarks>
public sealed class EfQueryExecutor : IQueryExecutor
{
    public Task<List<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
        => query.ToListAsync(cancellationToken);

    public Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
        => query.CountAsync(cancellationToken);

    public Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default)
        => query.FirstOrDefaultAsync(cancellationToken);
}
