using Microsoft.AspNetCore.Mvc;
using Scootly.Application.Abstractions;

namespace Scootly.Mvc.ViewComponents;

public sealed class ActiveRidesViewComponent : ViewComponent
{
    /// <summary>Panel kartı en fazla bu kadar kaydı sayar (daha fazlası "100+" olarak gösterilir).</summary>
    public const int ListLimit = 100;

    private readonly IRideReadService _rideReadService;

    public ActiveRidesViewComponent(IRideReadService rideReadService)
    {
        _rideReadService = rideReadService;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var rides = await _rideReadService.GetActiveRidesAsync(ListLimit);
        return View(rides);
    }
}
