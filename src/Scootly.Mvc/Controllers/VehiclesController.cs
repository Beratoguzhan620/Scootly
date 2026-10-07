using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scootly.Application.Abstractions;
using Scootly.Application.Fleet.Commands;
using Scootly.Domain.Common;
using Scootly.Infrastructure.Authorization;
using Scootly.Infrastructure.Caching;
using Scootly.Mvc.ViewModels;

namespace Scootly.Mvc.Controllers;

/// <summary>
/// Filo listesi ve araç işlemleri (yalnızca filo ekibi). Liste tüm araçların konumunu ve durumunu gösterdiği için
/// sürücülere açık değildir; düzenleme ve kayıt ayrıca filo yöneticisi ister.
/// </summary>
[Authorize(Policy = PolicyNames.FleetOperations)]
public sealed class VehiclesController : Controller
{
    public const int DefaultPageSize = 20;
    public const int MaxPageSize = 100;
    public const int MaxPageNumber = 10_000;

    private readonly IVehicleReadService _readService;
    private readonly RegisterVehicleCommandHandler _registerHandler;
    private readonly UpdateVehicleDetailsCommandHandler _updateHandler;
    private readonly VehicleMaintenanceCommandHandler _maintenanceHandler;
    private readonly NearbyVehicleCache _publicListCache;

    public VehiclesController(
        IVehicleReadService readService,
        RegisterVehicleCommandHandler registerHandler,
        UpdateVehicleDetailsCommandHandler updateHandler,
        VehicleMaintenanceCommandHandler maintenanceHandler,
        NearbyVehicleCache publicListCache)
    {
        _readService = readService;
        _registerHandler = registerHandler;
        _updateHandler = updateHandler;
        _maintenanceHandler = maintenanceHandler;
        _publicListCache = publicListCache;
    }

    public async Task<IActionResult> Index(int pageNumber = 1, int pageSize = DefaultPageSize, CancellationToken cancellationToken = default)
    {
        // Elle yazılmış ya da bozuk sayfa parametreleri hata sayfası yerine en yakın geçerli değere çekilir.
        pageNumber = Math.Clamp(pageNumber, 1, MaxPageNumber);
        pageSize = Math.Clamp(pageSize, 1, MaxPageSize);

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
            ModelState.AddModelError(string.Empty, result.Error);
            return View(model);
        }

        await _publicListCache.InvalidateAsync(CacheKeys.NearbyVehiclesDefault(), cancellationToken);

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
            ModelState.AddModelError(string.Empty, result.Error);
            return View(model);
        }

        TempData["SuccessMessage"] = "Araç başarıyla güncellendi.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Aracı kiralamadan çekip bakıma alır (örn. saha kontrolü gerektiğinde).</summary>
    [Authorize(Policy = PolicyNames.FleetOperations)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> SendToMaintenance(Guid id, int pageNumber = 1, CancellationToken cancellationToken = default)
        => ChangeStatusAsync(token => _maintenanceHandler.Handle(new SendVehicleToMaintenanceCommand(id), token),
            "Araç bakıma alındı.", pageNumber, cancellationToken);

    /// <summary>Bulunamayan aracı kayıp olarak işaretler; bulununca "Hizmete döndür" ile geri gelir.</summary>
    [Authorize(Policy = PolicyNames.FleetOperations)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> MarkLost(Guid id, int pageNumber = 1, CancellationToken cancellationToken = default)
        => ChangeStatusAsync(token => _maintenanceHandler.Handle(new MarkVehicleLostCommand(id), token),
            "Araç kayıp olarak işaretlendi.", pageNumber, cancellationToken);

    /// <summary>
    /// Bakımdaki veya kayıp aracı yeniden kiralanabilir yapar. Terk edilen sürüşlerden sonra bakıma alınan araçlar
    /// denetim görevi tamamlandıktan sonra buradan hizmete döndürülür.
    /// </summary>
    [Authorize(Policy = PolicyNames.FleetOperations)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public Task<IActionResult> ReturnToService(Guid id, int pageNumber = 1, CancellationToken cancellationToken = default)
        => ChangeStatusAsync(token => _maintenanceHandler.Handle(new ReturnVehicleToServiceCommand(id), token),
            "Araç hizmete döndürüldü.", pageNumber, cancellationToken);

    private async Task<IActionResult> ChangeStatusAsync(
        Func<CancellationToken, Task<Result>> change,
        string successMessage,
        int pageNumber,
        CancellationToken cancellationToken)
    {
        var result = await change(cancellationToken);

        if (result.IsSuccess)
        {
            await _publicListCache.InvalidateAsync(CacheKeys.NearbyVehiclesDefault(), cancellationToken);
            TempData["SuccessMessage"] = successMessage;
        }
        else
        {
            TempData["ErrorMessage"] = result.Error;
        }

        return RedirectToAction(nameof(Index), new { pageNumber = Math.Clamp(pageNumber, 1, MaxPageNumber) });
    }
}
