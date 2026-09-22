using Scootly.Application.Common;
using Scootly.Application.Fleet.Queries;

namespace Scootly.Application.Abstractions;

/// <summary>
/// Harita sorgusunun önbelleği (48. gün — cache-aside).
/// </summary>
/// <remarks>
/// <para>
/// Genel <see cref="ICacheService"/> varken bu ayrı arayüzün olmasının sebebi,
/// anahtar üretimi ve yaşam süresi kararının <b>tek bir yerde</b> durması.
/// Handler'ın içine <c>"yakin:" + lat + ":" + lon</c> gibi bir satır yazmak,
/// aynı anahtarı üreten ikinci bir yer (geçersizleştirme tarafı) ortaya
/// çıktığı gün ikisinin sessizce ayrışmasıyla biter: yazan bir anahtar
/// kullanır, silen başka bir anahtar siler, ve önbellek hiç temizlenmez.
/// </para>
/// <para>
/// Geçersizleştirme <see cref="InvalidateAsync"/> üzerinden. Hangi olayda
/// neyin silineceği ADR 0017'deki tabloda.
/// </para>
/// </remarks>
public interface INearbyVehicleCache
{
    /// <summary>
    /// Önbellekte varsa döndürür; yoksa <paramref name="fromDatabase"/> ile
    /// üretip önbelleğe yazar ve döndürür.
    /// </summary>
    Task<PagedResult<NearbyVehicleDto>> GetOrSetAsync(
        FindNearbyVehiclesQuery query,
        Func<CancellationToken, Task<PagedResult<NearbyVehicleDto>>> fromDatabase,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Bir aracın durumu değiştiğinde onu içerebilecek önbellek girdilerini
    /// geçersiz kılar.
    /// </summary>
    Task InvalidateAsync(CancellationToken cancellationToken = default);
}
