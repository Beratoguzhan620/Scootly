namespace Scootly.Application.Abstractions;

public interface IRegionResolver
{
    /// <summary>Hiçbir hizmet bölgesine düşmeyen konumlar için kullanılan bölge adı.</summary>
    public const string DefaultRegion = "default-region";

    Task<string> ResolveRegionAsync(double latitude, double longitude, CancellationToken cancellationToken = default);

    Task<bool> IsKnownRegionAsync(string regionName, CancellationToken cancellationToken = default);
}
