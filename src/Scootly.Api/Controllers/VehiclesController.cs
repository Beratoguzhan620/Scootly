using Microsoft.AspNetCore.Mvc;
using Scootly.Api.Contracts.Requests;
using Scootly.Api.Contracts.Responses;
using Scootly.Application.Abstractions;
using Scootly.Application.Riding.Commands;
using Scootly.Domain.Fleet;
using Scootly.Domain.Geo;

namespace Scootly.Api.Controllers;

[ApiController]
[Route("api/vehicles")]
public sealed class VehiclesController : ControllerBase
{
    private readonly IApplicationDbContext _context;
    private readonly ReserveVehicleCommandHandler _reserveHandler;

    public VehiclesController(IApplicationDbContext context, ReserveVehicleCommandHandler reserveHandler)
    {
        _context = context;
        _reserveHandler = reserveHandler;
    }

    [HttpGet("nearby")]
    public ActionResult<IReadOnlyList<VehicleResponse>> GetNearby(
        [FromQuery] double latitude,
        [FromQuery] double longitude,
        [FromQuery] double radiusMeters = 1000)
    {
        var origin = new GeoPoint(latitude, longitude);

        var vehicles = _context.Vehicles
            .Where(v => v.Status == VehicleStatus.Available)
            .AsEnumerable()
            .Where(v => v.Location.DistanceTo(origin) <= radiusMeters)
            .Select(v => new VehicleResponse(
                v.Id,
                v.Location.Latitude,
                v.Location.Longitude,
                v.Battery.Percentage,
                v.Status.ToString()))
            .ToList();

        return Ok(vehicles);
    }

    [HttpPost("{id:guid}/reserve")]
    public async Task<IActionResult> Reserve(
        Guid id,
        [FromBody] ReserveVehicleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await _reserveHandler.Handle(
            new ReserveVehicleCommand(id, request.DriverId),
            cancellationToken);

        if (!result.IsSuccess)
            return NotFound(result.Error);

        return NoContent();
    }
}