using Scootly.Application.Abstractions;
using Scootly.Application.Common;
using Scootly.Application.Fleet.Queries;

namespace Scootly.DbLab;

/// <summary>
/// Hiçbir şey önbelleklemeyen <see cref="INearbyVehicleCache"/>.
/// </summary>
/// <remarks>
/// Laboratuvar deneyleri veritabanı davranışını ölçüyor. Araya bir önbellek
/// girseydi, 38. günün "elli istekten tam olarak biri kazanır" ölçümü artık
/// eşzamanlılık korumasını değil, önbellek isabet oranını ölçerdi.
/// </remarks>
internal sealed class OnbelleksizHaritaOnbellegi : INearbyVehicleCache
{
    public Task<PagedResult<NearbyVehicleDto>> GetOrSetAsync(
        FindNearbyVehiclesQuery query,
        Func<CancellationToken, Task<PagedResult<NearbyVehicleDto>>> fromDatabase,
        CancellationToken cancellationToken = default)
        => fromDatabase(cancellationToken);

    public Task InvalidateAsync(CancellationToken cancellationToken = default)
        => Task.CompletedTask;
}
