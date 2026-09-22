namespace Scootly.Application.Abstractions;

/// <summary>
/// Bir <see cref="IQueryable{T}"/>'ı asenkron olarak çalıştırır.
/// </summary>
/// <remarks>
/// <para>
/// Bu arayüzün var olma sebebi tek bir satır: <c>ToListAsync()</c>. O metot
/// LINQ'ün değil, <c>Microsoft.EntityFrameworkCore</c> paketinin bir uzantı
/// metodu. Sorgu handler'ları Application katmanında olduğuna göre, onu
/// doğrudan çağırmak Application projesini EF Core'a bağımlı yapardı.
/// </para>
/// <para>
/// Bu, Karar 1'in ("Application somut teknolojiden bağımsız olmalı") en sinsi
/// ihlali: paket referansı tek bir <c>using</c> satırıyla girer, kimse fark
/// etmez, ve o noktadan sonra Application'ın içinde EF tiplerinin görünmesi
/// normalleşir. 38. günde aynı sınırı <c>DbUpdateConcurrencyException</c> için
/// korumuştuk (bkz. <see cref="Common.ConcurrencyConflictException"/>);
/// burada da aynı nedenle koruyoruz.
/// </para>
/// <para>
/// Bedeli dürüstçe: fazladan bir arayüz ve her sorguda bir dolaylılık katmanı.
/// Kazancı: Application projesinin paket listesi Domain'inki kadar sade kalıyor
/// ve sorgu handler'ları EF olmadan test edilebiliyor — bellekteki bir liste
/// <c>AsQueryable()</c> ile verilip sahte bir yürütücüyle çalıştırılabiliyor.
/// </para>
/// </remarks>
public interface IQueryExecutor
{
    Task<List<T>> ToListAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default);

    Task<int> CountAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default);

    Task<T?> FirstOrDefaultAsync<T>(IQueryable<T> query, CancellationToken cancellationToken = default);
}
