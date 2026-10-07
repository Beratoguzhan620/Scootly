using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scootly.Application.Abstractions;
using Scootly.Infrastructure.Authorization;
using Scootly.Infrastructure.Identity;

namespace Scootly.Mvc.Controllers;

/// <summary>
/// Canlı araç haritası. Araç konumları Mvc'nin kendi ucundan (cookie ile, rol bazlı) okunur: filo ekibi tüm
/// araçları, diğer kullanıcılar yalnızca müsait araçları görür. Durum değişiklikleri SignalR ile gelir; bunun için
/// sayfaya yalnızca hub'da geçerli, kısa ömürlü bir token verilir (API uçlarında geçersizdir).
/// </summary>
[Authorize]
public sealed class MapController : Controller
{
    /// <summary>Haritaya tek seferde çizilecek en fazla araç (tarayıcıyı yormamak için üst sınır).</summary>
    public const int MaxVehicles = 5_000;

    private readonly UserManager<ApplicationUser> _userManager;
    private readonly HubTokenGenerator _hubTokenGenerator;
    private readonly IApplicationDbContext _dbContext;
    private readonly IVehicleReadService _vehicleReadService;
    private readonly IAuthorizationService _authorizationService;

    public MapController(
        UserManager<ApplicationUser> userManager,
        HubTokenGenerator hubTokenGenerator,
        IApplicationDbContext dbContext,
        IVehicleReadService vehicleReadService,
        IAuthorizationService authorizationService)
    {
        _userManager = userManager;
        _hubTokenGenerator = hubTokenGenerator;
        _dbContext = dbContext;
        _vehicleReadService = vehicleReadService;
        _authorizationService = authorizationService;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var user = await _userManager.GetUserAsync(User);

        if (user is null)
            return Challenge();

        // Bildirimler aracın bulunduğu hizmet bölgesinin grubuna gider; harita tüm bölgeleri dinler.
        var regions = await _dbContext.ServiceAreas
            .AsNoTracking()
            .OrderBy(a => a.Name)
            .Select(a => a.Name)
            .ToListAsync(cancellationToken);

        ViewBag.HubToken = _hubTokenGenerator.Generate(user);
        ViewBag.Regions = JsonSerializer.Serialize(regions.Prepend(IRegionResolver.DefaultRegion));
        ViewBag.FleetView = await CanSeeWholeFleetAsync();

        return View();
    }

    [HttpGet]
    public async Task<IActionResult> Vehicles(CancellationToken cancellationToken)
    {
        var vehicles = await _vehicleReadService.GetForMapAsync(
            onlyAvailable: !await CanSeeWholeFleetAsync(), MaxVehicles, cancellationToken);

        Response.Headers.CacheControl = "no-store";

        return Json(vehicles.Select(v => new { v.Id, v.Latitude, v.Longitude, v.BatteryPercentage, v.Status }));
    }

    private async Task<bool> CanSeeWholeFleetAsync()
        => (await _authorizationService.AuthorizeAsync(User, PolicyNames.FleetOperations)).Succeeded;
}
