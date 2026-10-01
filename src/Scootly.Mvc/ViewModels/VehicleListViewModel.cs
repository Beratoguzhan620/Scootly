namespace Scootly.Mvc.ViewModels;

public sealed record VehicleListItemViewModel(
    Guid Id,
    string ShortId,
    double Latitude,
    double Longitude,
    int BatteryPercentage,
    string Status,
    string Brand,
    int RangeKm);

public sealed class VehicleListViewModel
{
    public required IReadOnlyList<VehicleListItemViewModel> Items { get; init; }
    public required int PageNumber { get; init; }
    public required int PageSize { get; init; }
    public required int TotalCount { get; init; }

    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
}