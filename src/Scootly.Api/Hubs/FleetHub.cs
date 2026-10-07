using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Scootly.Api.Extensions;
using Scootly.Application.Abstractions;

namespace Scootly.Api.Hubs;

/// <summary>
/// Canlı araç durumu bildirimleri. Yalnızca oturum açmış istemciler bağlanabilir ve
/// yalnızca var olan hizmet bölgelerinin gruplarına katılabilir. Normal API token'ının yanında Mvc'nin
/// yalnızca hub için ürettiği token da kabul edilir (bkz. <see cref="SecurityExtensions.HubScheme"/>).
/// </summary>
[Authorize(AuthenticationSchemes = SecurityExtensions.HubSchemes)]
public sealed class FleetHub : Hub
{
    public const int RegionNameMaxLength = 200;

    private readonly IRegionResolver _regionResolver;

    public FleetHub(IRegionResolver regionResolver)
    {
        _regionResolver = regionResolver;
    }

    public async Task JoinRegion(string regionName)
    {
        if (string.IsNullOrWhiteSpace(regionName) || regionName.Length > RegionNameMaxLength
            || !await _regionResolver.IsKnownRegionAsync(regionName, Context.ConnectionAborted))
        {
            throw new HubException("Bilinmeyen bölge.");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, regionName, Context.ConnectionAborted);
    }

    public async Task LeaveRegion(string regionName)
    {
        if (string.IsNullOrWhiteSpace(regionName) || regionName.Length > RegionNameMaxLength)
            return;

        await Groups.RemoveFromGroupAsync(Context.ConnectionId, regionName, Context.ConnectionAborted);
    }
}
