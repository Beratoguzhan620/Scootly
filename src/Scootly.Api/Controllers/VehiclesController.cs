using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scootly.Api.Authorization;
using Scootly.Application.Abstractions;
using Scootly.Application.Riding.Commands;
using Scootly.Api.Contracts.Requests;
using Scootly.Api.Contracts.Responses;
using Scootly.Domain.Fleet;
using Scootly.Domain.Geo;

namespace Scootly.Api.Controllers;

[ApiController]
[Route("api/vehicles")]
public sealed class VehiclesController : ControllerBase
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ReserveVehicleCommandHandler _reserveHandler;

    public VehiclesController(
        IApplicationDbContext dbContext,
        ReserveVehicleCommandHandler reserveHandler)
    {
        _dbContext = dbContext;
        _reserveHandler = reserveHandler;
    }

    [HttpGet]
    [AllowAnonymous]
    public IActionResult GetNearby()
    {
        var vehicles = _dbContext.Vehicles
            .Select(v => new VehicleResponse(
                v.Id,
                v.Location.Latitude,
                v.Location.Longitude,
                v.Battery.Percentage,
                v.Status.ToString()))
            .ToList();

        return Ok(vehicles);
    }

    [HttpPost]
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
    [Authorize]
    public async Task<IActionResult> Reserve(Guid id, [FromBody] ReserveVehicleRequest request)
    {
        var command = new ReserveVehicleCommand(id, request.DriverId);
        var result = await _reserveHandler.Handle(command);

        if (!result.IsSuccess)
            return Conflict(result.Error);

        return Ok();
    }
}