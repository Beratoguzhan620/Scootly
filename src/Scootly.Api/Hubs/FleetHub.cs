using Microsoft.AspNetCore.SignalR;

namespace Scootly.Api.Hubs;

public sealed class FleetHub : Hub
{
    public async Task JoinRegion(string regionName)
    {
        await Groups.AddToGroupAsync(Context.ConnectionId, regionName);
    }

    public async Task LeaveRegion(string regionName)
    {
        await Groups.RemoveFromGroupAsync(Context.ConnectionId, regionName);
    }
}