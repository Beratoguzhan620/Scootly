using Microsoft.AspNetCore.Mvc;
using Scootly.Application.Abstractions;

namespace Scootly.Mvc.ViewComponents;

public sealed class ActiveRidesViewComponent : ViewComponent
{
    private readonly IRideReadService _rideReadService;

    public ActiveRidesViewComponent(IRideReadService rideReadService)
    {
        _rideReadService = rideReadService;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var rides = await _rideReadService.GetActiveRidesAsync();
        return View(rides);
    }
}