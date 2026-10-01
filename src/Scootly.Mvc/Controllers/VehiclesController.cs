using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scootly.Application.Abstractions;
using Scootly.Mvc.ViewModels;

namespace Scootly.Mvc.Controllers;

[Authorize]
public sealed class VehiclesController : Controller
{
    private readonly IVehicleReadService _readService;

    public VehiclesController(IVehicleReadService readService)
    {
        _readService = readService;
    }

    public async Task<IActionResult> Index(int pageNumber = 1, int pageSize = 20, CancellationToken cancellationToken = default)
    {
        var filter = new VehicleFilter(null, null, null, null, false, pageNumber, pageSize);
        var page = await _readService.GetVehiclesAsync(filter, cancellationToken);

        var viewModel = new VehicleListViewModel
        {
            Items = page.Items
                .Select(v => new VehicleListItemViewModel(
                    v.Id, v.Id.ToString()[..8], v.Latitude, v.Longitude, v.BatteryPercentage, v.Status, v.Brand, v.RangeKm))
                .ToList(),
            PageNumber = page.PageNumber,
            PageSize = page.PageSize,
            TotalCount = page.TotalCount
        };

        return View(viewModel);
    }
}