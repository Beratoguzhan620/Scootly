using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Scootly.Api.Authorization;
using Scootly.Api.Contracts.Requests;
using Scootly.Api.Contracts.Responses;
using Scootly.Application.Abstractions;
using Scootly.Application.Riding.Commands;
using Scootly.Domain.Fleet;
using Scootly.Domain.Geo;
using Scootly.Infrastructure.Caching;

namespace Scootly.Api.Controllers;

[ApiController]
[ApiVersion("1.0")]
[ApiVersion("2.0")]
[Route("api/v{version:apiVersion}/vehicles")]
public sealed class VehiclesController : ControllerBase
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ReserveVehicleCommandHandler _reserveHandler;
    private readonly ICurrentUser _currentUser;
    private readonly NearbyVehicleCache _cache;

    public VehiclesController(
        IApplicationDbContext dbContext,
        ReserveVehicleCommandHandler reserveHandler,
        ICurrentUser currentUser,
        NearbyVehicleCache cache)
    {
        _dbContext = dbContext;
        _reserveHandler = reserveHandler;
        _currentUser = currentUser;
        _cache = cache;
    }

    [HttpGet]
    [MapToApiVersion("1.0")]
    [AllowAnonymous]
    [EnableRateLimiting("AnonymousPolicy")]
    public async Task<IActionResult> GetNearbyV1(
        [FromQuery] double? minLatitude = null,
        [FromQuery] double? maxLatitude = null,
        [FromQuery] double? minLongitude = null,
        [FromQuery] double? maxLongitude = null,
        [FromQuery] bool onlyAvailable = false,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 20)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize is < 1 or > 100) pageSize = 20;

        var isDefaultQuery = !minLatitude.HasValue && !maxLatitude.HasValue
            && !minLongitude.HasValue && !maxLongitude.HasValue
            && !onlyAvailable && pageNumber == 1 && pageSize == 20;

        if (isDefaultQuery)
        {
            var cached = await _cache.GetOrSetAsync(
                CacheKeys.NearbyVehiclesDefault(),
                () => Task.FromResult(RunQuery(null, null, null, null, false, 1, 20)));

            return Ok(cached);
        }

        var result = RunQuery(minLatitude, maxLatitude, minLongitude, maxLongitude, onlyAvailable, pageNumber, pageSize);
        return Ok(result);
    }

    private PagedResult<VehicleResponse> RunQuery(
        double? minLatitude, double? maxLatitude, double? minLongitude, double? maxLongitude,
        bool onlyAvailable, int pageNumber, int pageSize)
    {
        var baseQuery = _dbContext.Vehicles.AsNoTracking();

        if (minLatitude.HasValue) baseQuery = baseQuery.Where(v => v.Location.Latitude >= minLatitude.Value);
        if (maxLatitude.HasValue) baseQuery = baseQuery.Where(v => v.Location.Latitude <= maxLatitude.Value);
        if (minLongitude.HasValue) baseQuery = baseQuery.Where(v => v.Location.Longitude >= minLongitude.Value);
        if (maxLongitude.HasValue) baseQuery = baseQuery.Where(v => v.Location.Longitude <= maxLongitude.Value);
        if (onlyAvailable) baseQuery = baseQuery.Where(v => v.Status == VehicleStatus.Available);

        var query = baseQuery.Select(v => new VehicleResponse(
            v.Id, v.Location.Latitude, v.Location.Longitude, v.Battery.Percentage, v.Status.ToString()));

        var totalCount = query.Count();
        var items = query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

        return new PagedResult<VehicleResponse>(items, pageNumber, pageSize, totalCount);
    }

    [HttpGet]
    [MapToApiVersion("2.0")]
    [AllowAnonymous]
    public IActionResult GetNearbyV2([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 20)
    {
        if (pageNumber < 1) pageNumber = 1;
        if (pageSize is < 1 or > 100) pageSize = 20;

        var query = _dbContext.Vehicles
            .AsNoTracking()
            .Select(v => new VehicleResponseV2(
                v.Id, v.Location.Latitude, v.Location.Longitude, v.Battery.Percentage,
                v.Status.ToString(), v.Model.Brand, v.Model.RangeKm));

        var totalCount = query.Count();
        var items = query.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

        return Ok(new PagedResult<VehicleResponseV2>(items, pageNumber, pageSize, totalCount));
    }

    [HttpPost]
    [MapToApiVersion("1.0")]
    [MapToApiVersion("2.0")]
    [Authorize(Policy = PolicyNames.FleetManagerOnly)]
    public IActionResult Register([FromBody] RegisterVehicleRequest request)
    {
        var vehicle = new Vehicle(
            VehicleId.New(),
            new VehicleModel(request.Brand, request.RangeKm),
            new GeoPoint(request.Latitude, request.Longitude),
            new BatteryLevel(request.BatteryPercentage));

        _dbContext.AddVehicle(vehicle);

        return Ok(vehicle.Id);
    }

    [HttpPost("{id}/reserve")]
    [MapToApiVersion("1.0")]
    [MapToApiVersion("2.0")]
    [Authorize]
    public async Task<IActionResult> Reserve(Guid id)
    {
        var command = new ReserveVehicleCommand(id, _currentUser.UserId);
        var result = await _reserveHandler.Handle(command);

        if (!result.IsSuccess)
            return Conflict(result.Error);

        await _cache.InvalidateAsync(CacheKeys.NearbyVehiclesDefault());

        return Ok();
    }
}