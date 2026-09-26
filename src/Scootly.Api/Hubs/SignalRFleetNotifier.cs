using Microsoft.AspNetCore.SignalR;
using Scootly.Application.Abstractions;

namespace Scootly.Api.Hubs;

public sealed class SignalRFleetNotifier : IFleetNotifier
{
    private readonly IHubContext<FleetHub> _hubContext;

    public SignalRFleetNotifier(IHubContext<FleetHub> hubContext)
    {
        _hubContext = hubContext;
    }

    public async Task NotifyVehicleStatusChangedAsync(
        Guid vehicleId, string regionName, string newStatus, CancellationToken cancellationToken = default)
    {
        await _hubContext.Clients.Group(regionName).SendAsync(
            "VehicleStatusChanged",
            new { VehicleId = vehicleId, Status = newStatus },
            cancellationToken);
    }
}