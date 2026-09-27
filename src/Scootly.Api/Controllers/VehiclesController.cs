using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Scootly.Api.Authorization;
using Scootly.Api.Contracts.Requests;
using Scootly.Api.Contracts.Responses;
using Scootly.Api.ErrorHandling;
using Scootly.Api.Extensions;
using Scootly.Api.Validators;
using Scootly.Application.Abstractions;
using Scootly.Application.Fleet.Commands;
using Scootly.Application.Riding.Commands;
using Scootly.Domain.Fleet;
using Scootly.Infrastructure.Caching;

namespace Scootly.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/vehicles")]
public sealed class VehiclesController : ControllerBase
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ICurrentUser _currentUser;
    private readonly NearbyVehicleCache _cache;
    private readonly VehicleQueryValidator _queryValidator;

    public VehiclesController(
        IApplicationDbContext dbContext,
        ICurrentUser currentUser,
        NearbyVehicleCache cache,
        VehicleQueryValidator queryValidator)
    {
        _dbContext = dbContext;
        _currentUser = currentUser;
        _cache = cache;
        _queryValidator = queryValidator;
    }

    [HttpGet]
    [MapToApiVersion("1.0")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Anonymous)]
    [ProducesResponseType<PagedResult<VehicleResponse>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetNearbyV1(
        [FromQuery] double? minLatitude = null,
        [FromQuery] double? maxLatitude = null,
        [FromQuery] double? minLongitude = null,
        [FromQuery] double? maxLongitude = null,
        [FromQuery] bool onlyAvailable = false,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var query = new VehicleQuery(minLatitude, maxLatitude, minLongitude, maxLongitude, onlyAvailable, pageNumber, pageSize);
        var validation = _queryValidator.Validate(query);

        if (!validation.IsValid)
            return this.BadRequestProblem(validation.Error!);

        var isDefaultQuery = query == new VehicleQuery(null, null, null, null, false, 1, 20);

        if (isDefaultQuery)
        {
            var cached = await _cache.GetOrSetAsync(CacheKeys.NearbyVehiclesDefault(), token => RunQueryAsync(query, token), cancellationToken);
            return Ok(cached);
        }

        return Ok(await RunQueryAsync(query, cancellationToken));
    }

    [HttpGet]
    [MapToApiVersion("2.0")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Anonymous)]
    [ProducesResponseType<PagedResult<VehicleResponseV2>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetNearbyV2(
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var validation = _queryValidator.Validate(new VehicleQuery(null, null, null, null, false, pageNumber, pageSize));

        if (!validation.IsValid)
            return this.BadRequestProblem(validation.Error!);

        var query = _dbContext.Vehicles
            .AsNoTracking()
            .OrderBy(v => v.Id)
            .Select(v => new VehicleResponseV2(
                v.Id, v.Location.Latitude, v.Location.Longitude, v.Battery.Percentage,
                v.Status.ToString(), v.Model.Brand, v.Model.RangeKm));

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToListAsync(cancellationToken);

        return Ok(new PagedResult<VehicleResponseV2>(items, pageNumber, pageSize, totalCount));
    }

    [HttpGet("{id:guid}")]
    [MapToApiVersion("1.0")]
    [MapToApiVersion("2.0")]
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Anonymous)]
    [ProducesResponseType<VehicleResponseV2>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var vehicle = await _dbContext.Vehicles
            .AsNoTracking()
            .Where(v => v.Id == id)
            .Select(v => new VehicleResponseV2(
                v.Id, v.Location.Latitude, v.Location.Longitude, v.Battery.Percentage,
                v.Status.ToString(), v.Model.Brand, v.Model.RangeKm))
            .FirstOrDefaultAsync(cancellationToken);

        return vehicle is null ? NotFound() : Ok(vehicle);
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
    public async Task<IActionResult> SendToMaintenance(
        Guid id,
        [FromServices] VehicleMaintenanceCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new SendVehicleToMaintenanceCommand(id), cancellationToken);

        if (!result.IsSuccess)
            return this.ToProblem(result);

        await _cache.InvalidateAsync(CacheKeys.NearbyVehiclesDefault(), cancellationToken);

        return NoContent();
    }

    [HttpPost("{id:guid}/return-to-service")]
    [MapToApiVersion("1.0")]
    [MapToApiVersion("2.0")]
    [Authorize(Policy = PolicyNames.FleetOperations)]
    [EnableRateLimiting(RateLimitPolicies.User)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ReturnToService(
        Guid id,
        [FromServices] VehicleMaintenanceCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new ReturnVehicleToServiceCommand(id), cancellationToken);

        if (!result.IsSuccess)
            return this.ToProblem(result);

        await _cache.InvalidateAsync(CacheKeys.NearbyVehiclesDefault(), cancellationToken);

        return NoContent();
    }

    private async Task<PagedResult<VehicleResponse>> RunQueryAsync(VehicleQuery filter, CancellationToken cancellationToken)
    {
        var baseQuery = _dbContext.Vehicles.AsNoTracking();

        if (filter.MinLatitude is { } minLatitude) baseQuery = baseQuery.Where(v => v.Location.Latitude >= minLatitude);
        if (filter.MaxLatitude is { } maxLatitude) baseQuery = baseQuery.Where(v => v.Location.Latitude <= maxLatitude);
        if (filter.MinLongitude is { } minLongitude) baseQuery = baseQuery.Where(v => v.Location.Longitude >= minLongitude);
        if (filter.MaxLongitude is { } maxLongitude) baseQuery = baseQuery.Where(v => v.Location.Longitude <= maxLongitude);
        if (filter.OnlyAvailable) baseQuery = baseQuery.Where(v => v.Status == VehicleStatus.Available);

        var query = baseQuery
            .OrderBy(v => v.Id)
            .Select(v => new VehicleResponse(
                v.Id, v.Location.Latitude, v.Location.Longitude, v.Battery.Percentage, v.Status.ToString()));

        var totalCount = await query.CountAsync(cancellationToken);
        var items = await query
            .Skip((filter.PageNumber - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<VehicleResponse>(items, filter.PageNumber, filter.PageSize, totalCount);
    }
}
