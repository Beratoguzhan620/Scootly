namespace Scootly.Application.Abstractions;

public sealed record VehicleFilter(
    double? MinLatitude,
    double? MaxLatitude,
    double? MinLongitude,
    double? MaxLongitude,
    bool OnlyAvailable,
    int PageNumber,
    int PageSize);

public sealed record VehicleSummary(
    Guid Id,
    double Latitude,
    double Longitude,
    int BatteryPercentage,
    string Status,
    string Brand,
    int RangeKm);

public sealed record PagedList<T>(IReadOnlyList<T> Items, int PageNumber, int PageSize, int TotalCount);

public interface IVehicleReadService
{
    Task<PagedList<VehicleSummary>> GetVehiclesAsync(VehicleFilter filter, CancellationToken cancellationToken = default);

    Task<VehicleSummary?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VehicleSummary>> GetLowBatteryVehiclesAsync(CancellationToken cancellationToken = default);
}