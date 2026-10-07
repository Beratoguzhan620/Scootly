using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Scootly.Api.Contracts.Requests;
using Scootly.Api.Contracts.Responses;
using Scootly.Api.ErrorHandling;
using Scootly.Api.Extensions;
using Scootly.Api.Validators;
using Scootly.Application.Abstractions;
using Scootly.Application.Fleet.Commands;
using Scootly.Application.Riding.Commands;
using Scootly.Domain.Fleet;
using Scootly.Domain.Riding;
using Scootly.Infrastructure.Authorization;
using Scootly.Infrastructure.Caching;

namespace Scootly.Api.Controllers;

/// <summary>
/// Araç listesi ve araç işlemleri. Görünürlük kuralı (KVKK): anonim kullanıcılar ve sürücüler yalnızca müsait
/// araçları görür; rezerve, sürüşteki, bakımdaki ve kayıp araçların konumu yalnızca filo ekibine ve cihaz ağ
/// geçidine açıktır. Aksi halde sürüşteki bir aracın konumunu yoklayarak sürücünün güzergâhı izlenebilirdi.
/// </summary>
[ApiController]
[ApiVersion("1.0")]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/vehicles")]
public sealed class VehiclesController : ControllerBase
{
    /// <summary>Önbelleğe alınan tek sorgu: anonim varsayılan liste (yalnızca müsait araçlar, ilk sayfa).</summary>
    private static readonly VehicleQuery PublicDefaultQuery = new(null, null, null, null, OnlyAvailable: true, PageNumber: 1, PageSize: 20);

    private readonly IApplicationDbContext _dbContext;
    private readonly IVehicleReadService _readService;
    private readonly ICurrentUser _currentUser;
    private readonly IAuthorizationService _authorizationService;
    private readonly NearbyVehicleCache _cache;
    private readonly VehicleQueryValidator _queryValidator;

    public VehiclesController(
        IApplicationDbContext dbContext,
        IVehicleReadService readService,
        ICurrentUser currentUser,
        IAuthorizationService authorizationService,
        NearbyVehicleCache cache,
        VehicleQueryValidator queryValidator)
    {
        _dbContext = dbContext;
        _readService = readService;
        _currentUser = currentUser;
        _authorizationService = authorizationService;
        _cache = cache;
        _queryValidator = queryValidator;
    }

    /// <summary>Dikdörtgen alan (bounding box) filtresiyle sayfalı araç listesi.</summary>
    [HttpGet]
    [MapToApiVersion("1.0")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Anonymous)]
    [ProducesResponseType<PagedResult<VehicleResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetListV1(
        [FromQuery] double? minLatitude = null,
        [FromQuery] double? maxLatitude = null,
        [FromQuery] double? minLongitude = null,
        [FromQuery] double? maxLongitude = null,
        [FromQuery] bool onlyAvailable = false,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var requested = new VehicleQuery(minLatitude, maxLatitude, minLongitude, maxLongitude, onlyAvailable, pageNumber, pageSize);
        var validation = _queryValidator.Validate(requested);

        if (!validation.IsValid)
            return this.BadRequestProblem(validation.Error!);

        var query = await ApplyVisibilityAsync(requested);

        if (query == PublicDefaultQuery)
        {
            var cached = await _cache.GetOrSetAsync(CacheKeys.NearbyVehiclesDefault(), token => RunQueryV1Async(query, token), cancellationToken);
            return Ok(cached);
        }

        return Ok(await RunQueryV1Async(query, cancellationToken));
    }

    /// <summary>v1 ile aynı filtreler; yanıtta model (marka, menzil) bilgisi de bulunur.</summary>
    [HttpGet]
    [MapToApiVersion("2.0")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Anonymous)]
    [ProducesResponseType<PagedResult<VehicleResponseV2>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetListV2(
        [FromQuery] double? minLatitude = null,
        [FromQuery] double? maxLatitude = null,
        [FromQuery] double? minLongitude = null,
        [FromQuery] double? maxLongitude = null,
        [FromQuery] bool onlyAvailable = false,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var requested = new VehicleQuery(minLatitude, maxLatitude, minLongitude, maxLongitude, onlyAvailable, pageNumber, pageSize);
        var validation = _queryValidator.Validate(requested);

        if (!validation.IsValid)
            return this.BadRequestProblem(validation.Error!);

        var page = await _readService.GetVehiclesAsync(ToFilter(await ApplyVisibilityAsync(requested)), cancellationToken);

        var items = page.Items
            .Select(v => new VehicleResponseV2(v.Id, v.Latitude, v.Longitude, v.BatteryPercentage, v.Status, v.Brand, v.RangeKm))
            .ToList();

        return Ok(new PagedResult<VehicleResponseV2>(items, page.PageNumber, page.PageSize, page.TotalCount));
    }

    /// <summary>
    /// Araç ayrıntısı. Müsait olmayan bir araç yalnızca filo ekibine, cihaz ağ geçidine ve aracı rezerve etmiş ya da
    /// onunla sürüşte olan sürücüye gösterilir; diğerleri için araç yokmuş gibi 404 döner.
    /// </summary>
    [HttpGet("{id:guid}")]
    [MapToApiVersion("1.0")]
    [MapToApiVersion("2.0")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Anonymous)]
    [ProducesResponseType<VehicleResponseV2>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var vehicle = await _readService.GetByIdAsync(id, cancellationToken);

        if (vehicle is null)
            return NotFound();

        if (vehicle.Status != nameof(VehicleStatus.Available)
            && !await CanSeeWholeFleetAsync()
            && !await IsVehicleOfCurrentDriverAsync(id, cancellationToken))
        {
            return NotFound();
        }

        return Ok(new VehicleResponseV2(vehicle.Id, vehicle.Latitude, vehicle.Longitude, vehicle.BatteryPercentage, vehicle.Status, vehicle.Brand, vehicle.RangeKm));
    }

    [HttpPost]
    [MapToApiVersion("1.0")]
    [MapToApiVersion("2.0")]
    [Authorize(Policy = PolicyNames.FleetManagerOnly)]
    [EnableRateLimiting(RateLimitPolicies.User)]
    [ProducesResponseType<RegisterVehicleResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterVehicleRequest request,
        [FromServices] RegisterVehicleRequestValidator validator,
        [FromServices] RegisterVehicleCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var validation = validator.Validate(request);

        if (!validation.IsValid)
            return this.BadRequestProblem(validation.Error!);

        var result = await handler.Handle(
            new RegisterVehicleCommand(request.Brand, request.RangeKm, request.Latitude, request.Longitude, request.BatteryPercentage),
            cancellationToken);

        if (!result.IsSuccess)
            return this.ToProblem(result);

        await _cache.InvalidateAsync(CacheKeys.NearbyVehiclesDefault(), cancellationToken);

        return CreatedAtAction(
            nameof(GetById),
            new { id = result.Value, version = RouteData.Values["version"] },
            new RegisterVehicleResponse(result.Value));
    }

    [HttpPost("{id:guid}/reserve")]
    [MapToApiVersion("1.0")]
    [MapToApiVersion("2.0")]
    [Authorize(Policy = PolicyNames.DriverOnly)]
    [EnableRateLimiting(RateLimitPolicies.User)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Reserve(
        Guid id,
        [FromServices] ReserveVehicleCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new ReserveVehicleCommand(id, _currentUser.UserId), cancellationToken);

        if (!result.IsSuccess)
            return this.ToProblem(result);

        await _cache.InvalidateAsync(CacheKeys.NearbyVehiclesDefault(), cancellationToken);

        return Ok();
    }

    [HttpDelete("{id:guid}/reservation")]
    [MapToApiVersion("1.0")]
    [MapToApiVersion("2.0")]
    [Authorize(Policy = PolicyNames.DriverOnly)]
    [EnableRateLimiting(RateLimitPolicies.User)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CancelReservation(
        Guid id,
        [FromServices] CancelReservationCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new CancelReservationCommand(id, _currentUser.UserId), cancellationToken);

        if (!result.IsSuccess)
            return this.ToProblem(result);

        await _cache.InvalidateAsync(CacheKeys.NearbyVehiclesDefault(), cancellationToken);

        return NoContent();
    }

    [HttpPost("{id:guid}/maintenance")]
    [MapToApiVersion("1.0")]
    [MapToApiVersion("2.0")]
    [Authorize(Policy = PolicyNames.FleetOperations)]
    [EnableRateLimiting(RateLimitPolicies.User)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IActionResult> SendToMaintenance(
        Guid id,
        [FromServices] VehicleMaintenanceCommandHandler handler,
        CancellationToken cancellationToken)
        => ChangeStatusAsync(token => handler.Handle(new SendVehicleToMaintenanceCommand(id), token), cancellationToken);

    /// <summary>Bulunamayan (sinyal vermeyen, çalınmış) aracı kiralamadan çeker; bulununca return-to-service ile geri döner.</summary>
    [HttpPost("{id:guid}/lost")]
    [MapToApiVersion("1.0")]
    [MapToApiVersion("2.0")]
    [Authorize(Policy = PolicyNames.FleetOperations)]
    [EnableRateLimiting(RateLimitPolicies.User)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IActionResult> MarkLost(
        Guid id,
        [FromServices] VehicleMaintenanceCommandHandler handler,
        CancellationToken cancellationToken)
        => ChangeStatusAsync(token => handler.Handle(new MarkVehicleLostCommand(id), token), cancellationToken);

    [HttpPost("{id:guid}/return-to-service")]
    [MapToApiVersion("1.0")]
    [MapToApiVersion("2.0")]
    [Authorize(Policy = PolicyNames.FleetOperations)]
    [EnableRateLimiting(RateLimitPolicies.User)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public Task<IActionResult> ReturnToService(
        Guid id,
        [FromServices] VehicleMaintenanceCommandHandler handler,
        CancellationToken cancellationToken)
        => ChangeStatusAsync(token => handler.Handle(new ReturnVehicleToServiceCommand(id), token), cancellationToken);

    private async Task<IActionResult> ChangeStatusAsync(
        Func<CancellationToken, Task<Domain.Common.Result>> change,
        CancellationToken cancellationToken)
    {
        var result = await change(cancellationToken);

        if (!result.IsSuccess)
            return this.ToProblem(result);

        await _cache.InvalidateAsync(CacheKeys.NearbyVehiclesDefault(), cancellationToken);

        return NoContent();
    }

    /// <summary>Filo ekibi ve cihaz ağ geçidi dışındaki herkes için liste yalnızca müsait araçlarla sınırlanır.</summary>
    private async Task<VehicleQuery> ApplyVisibilityAsync(VehicleQuery requested)
        => requested.OnlyAvailable || await CanSeeWholeFleetAsync()
            ? requested
            : requested with { OnlyAvailable = true };

    private async Task<bool> CanSeeWholeFleetAsync()
    {
        if (User.Identity?.IsAuthenticated != true)
            return false;

        return (await _authorizationService.AuthorizeAsync(User, PolicyNames.FleetOperations)).Succeeded
               || (await _authorizationService.AuthorizeAsync(User, PolicyNames.DeviceOnly)).Succeeded;
    }

    private async Task<bool> IsVehicleOfCurrentDriverAsync(Guid vehicleId, CancellationToken cancellationToken)
    {
        if (!_currentUser.IsAuthenticated)
            return false;

        var driverId = _currentUser.UserId;

        return await _dbContext.Vehicles.AnyAsync(v => v.Id == vehicleId && v.ReservedBy == driverId, cancellationToken)
               || await _dbContext.Rides.AnyAsync(r => r.VehicleId == vehicleId && r.DriverId == driverId && r.Status == RideStatus.Active, cancellationToken);
    }

    private static VehicleFilter ToFilter(VehicleQuery query) => new(
        query.MinLatitude, query.MaxLatitude, query.MinLongitude, query.MaxLongitude,
        query.OnlyAvailable, query.PageNumber, query.PageSize);

    private async Task<PagedResult<VehicleResponse>> RunQueryV1Async(VehicleQuery query, CancellationToken cancellationToken)
    {
        var page = await _readService.GetVehiclesAsync(ToFilter(query), cancellationToken);

        var items = page.Items
            .Select(v => new VehicleResponse(v.Id, v.Latitude, v.Longitude, v.BatteryPercentage, v.Status))
            .ToList();

        return new PagedResult<VehicleResponse>(items, page.PageNumber, page.PageSize, page.TotalCount);
    }
}
