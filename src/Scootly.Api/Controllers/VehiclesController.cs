using Microsoft.AspNetCore.Mvc;
using Scootly.Application.Abstractions;
using Scootly.Application.Riding.Commands;
using Scootly.Api.Contracts.Requests;
using Scootly.Api.Contracts.Responses;

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

    [HttpPost("{id}/reserve")]
    public async Task<IActionResult> Reserve(Guid id, [FromBody] ReserveVehicleRequest request)
    {
        var command = new ReserveVehicleCommand(id, request.DriverId);
        var result = await _reserveHandler.Handle(command);

        if (!result.IsSuccess)
            return Conflict(result.Error);

        return Ok();
    }
}