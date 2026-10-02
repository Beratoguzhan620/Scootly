using Microsoft.AspNetCore.Mvc;
using Scootly.Application.Abstractions;

namespace Scootly.Mvc.ViewComponents;

public sealed class LowBatteryVehiclesViewComponent : ViewComponent
{
    private readonly IVehicleReadService _vehicleReadService;

    public LowBatteryVehiclesViewComponent(IVehicleReadService vehicleReadService)
    {
        _vehicleReadService = vehicleReadService;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var vehicles = await _vehicleReadService.GetLowBatteryVehiclesAsync();
        return View(vehicles);
    }
}