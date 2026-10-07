namespace Scootly.Application.Abstractions;

public sealed record ActiveRideSummary(
    Guid Id,
    Guid DriverId,
    Guid VehicleId,
    DateTime StartedAt);

public interface IRideReadService
{
    Task<IReadOnlyList<ActiveRideSummary>> GetActiveRidesAsync(int take, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ActiveRideSummary>> GetActiveRidesForDriverAsync(Guid driverId, CancellationToken cancellationToken = default);
}
