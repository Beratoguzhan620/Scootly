namespace Scootly.Application.Abstractions;

/// <summary>
/// Açık işlem (transaction) sınırı açar (35. gün).
/// </summary>
/// <remarks>
/// <para>
/// <see cref="IUnitOfWork"/>'e eklenmek yerine ayrı bir arayüz olması bilinçli.
/// İkisi farklı sorumluluk: "biriken değişiklikleri kaydet" ile "işlem sınırını
/// yönet" aynı şey değil. Çoğu komut handler'ının yalnızca birincisine ihtiyacı
/// var; ikisini tek arayüzde toplamak, hiç transaction açmayacak olan her
/// handler'ı ve her test sahtesini o metodu uygulamaya zorlardı.
/// </para>
/// <para>
/// <c>SaveChangesAsync</c> kendi başına da atomiktir — EF Core her çağrıyı
/// örtük bir transaction içine alır. Açık transaction, bir komutun
/// <b>birden fazla</b> kaydetme yaptığı ya da birden fazla toplam kökü
/// (aggregate root) aynı anda tutarlı bırakması gerektiği durumlar için.
/// </para>
/// </remarks>
public interface ITransactionManager
{
    Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken = default);
}
