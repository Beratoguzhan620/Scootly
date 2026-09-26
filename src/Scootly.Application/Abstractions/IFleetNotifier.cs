namespace Scootly.Application.Abstractions;

public interface IFleetNotifier
{
    Task NotifyVehicleStatusChangedAsync(Guid vehicleId, string regionName, string newStatus, CancellationToken cancellationToken = default);
}