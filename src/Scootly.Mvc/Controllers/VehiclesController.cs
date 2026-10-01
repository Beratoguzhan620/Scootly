using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scootly.Application.Abstractions;
using Scootly.Application.Fleet.Commands;
using Scootly.Infrastructure.Authorization;
using Scootly.Mvc.ViewModels;

namespace Scootly.Mvc.Controllers;

[Authorize]
public sealed class VehiclesController : Controller
{
    private readonly IVehicleReadService _readService;
    private readonly RegisterVehicleCommandHandler _registerHandler;
    private readonly UpdateVehicleDetailsCommandHandler _updateHandler;

    public VehiclesController(
        IVehicleReadService readService,
        RegisterVehicleCommandHandler registerHandler,
        UpdateVehicleDetailsCommandHandler updateHandler)
    {
        _readService = readService;
        _registerHandler = registerHandler;
        _updateHandler = updateHandler;
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

    [Authorize(Policy = PolicyNames.FleetManagerOnly)]
    [HttpGet]
    public IActionResult Create()
    {
        return View(new VehicleCreateViewModel());
    }

    [Authorize(Policy = PolicyNames.FleetManagerOnly)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(VehicleCreateViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(model);

        var result = await _registerHandler.Handle(
            new RegisterVehicleCommand(model.Brand, model.RangeKm, model.Latitude, model.Longitude, model.BatteryPercentage),
            cancellationToken);

        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Araç kaydedilemedi.");
            return View(model);
        }

        TempData["SuccessMessage"] = "Araç başarıyla kaydedildi.";
        return RedirectToAction(nameof(Index));
    }

    [Authorize(Policy = PolicyNames.FleetManagerOnly)]
    [HttpGet]
    public async Task<IActionResult> Edit(Guid id, CancellationToken cancellationToken)
    {
        var vehicle = await _readService.GetByIdAsync(id, cancellationToken);

        if (vehicle is null)
            return NotFound();

        var model = new VehicleEditViewModel
        {
            Id = vehicle.Id,
            Brand = vehicle.Brand,
            RangeKm = vehicle.RangeKm
        };

        return View(model);
    }

    [Authorize(Policy = PolicyNames.FleetManagerOnly)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(Guid id, VehicleEditViewModel model, CancellationToken cancellationToken)
    {
        if (id != model.Id)
            return BadRequest();

        if (!ModelState.IsValid)
            return View(model);

        var result = await _updateHandler.Handle(
            new UpdateVehicleDetailsCommand(model.Id, model.Brand, model.RangeKm),
            cancellationToken);

        if (!result.IsSuccess)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Araç güncellenemedi.");
            return View(model);
        }

        TempData["SuccessMessage"] = "Araç başarıyla güncellendi.";
        return RedirectToAction(nameof(Index));
    }
}