using Microsoft.AspNetCore.Mvc;
using Scootly.Application.Abstractions;

namespace Scootly.Mvc.ViewComponents;

public sealed class LowBatteryVehiclesViewComponent : ViewComponent
{
    /// <summary>Panel kartı en fazla bu kadar kaydı sayar (daha fazlası "100+" olarak gösterilir).</summary>
    public const int ListLimit = 100;

    private readonly IVehicleReadService _vehicleReadService;

    public LowBatteryVehiclesViewComponent(IVehicleReadService vehicleReadService)
    {
        _vehicleReadService = vehicleReadService;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var vehicles = await _vehicleReadService.GetLowBatteryVehiclesAsync(ListLimit);
        return View(vehicles);
    }
}
