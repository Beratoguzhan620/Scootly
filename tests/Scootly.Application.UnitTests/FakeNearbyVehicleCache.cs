using Scootly.Application.Abstractions;
using Scootly.Application.Common;
using Scootly.Application.Fleet.Queries;

namespace Scootly.Application.UnitTests;

/// <summary>
/// Önbellek yerine geçen sahte. Kaç kez geçersizleştirildiğini sayar.
/// </summary>
/// <remarks>
/// Sayaç süs değil: 49. günün asıl riski geçersizleştirmeyi UNUTMAK. Sahtenin
/// sayaç tutması, "bu komut önbelleği temizledi mi" sorusunu bir iddiaya
/// (assert) dönüştürülebilir kılıyor.
/// </remarks>
public sealed class FakeNearbyVehicleCache : INearbyVehicleCache
{
    public int GecersizlestirmeSayisi { get; private set; }

    public Task<PagedResult<NearbyVehicleDto>> GetOrSetAsync(
        FindNearbyVehiclesQuery query,
        Func<CancellationToken, Task<PagedResult<NearbyVehicleDto>>> fromDatabase,
        CancellationToken cancellationToken = default)
    {
        // Sahte önbellek hiç isabet üretmiyor: her çağrı veritabanına gidiyor.
        // Testlerin ölçtüğü şey önbellek değil, handler'ın davranışı.
        return fromDatabase(cancellationToken);
    }

    public Task InvalidateAsync(CancellationToken cancellationToken = default)
    {
        GecersizlestirmeSayisi++;
        return Task.CompletedTask;
    }
}
