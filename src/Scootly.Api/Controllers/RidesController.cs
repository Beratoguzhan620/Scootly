using Microsoft.AspNetCore.Mvc;
using Scootly.Api.Contracts.Requests;
using Scootly.Api.Validators;
using Scootly.Application.Riding.Commands;

namespace Scootly.Api.Controllers;

[ApiController]
[Route("api/rides")]
public sealed class RidesController : ControllerBase
{
    private readonly StartRideCommandHandler _startHandler;
    private readonly CompleteRideCommandHandler _completeHandler;
    private readonly StartRideRequestValidator _validator;

    public RidesController(
        StartRideCommandHandler startHandler,
        CompleteRideCommandHandler completeHandler,
        StartRideRequestValidator validator)
    {
        _startHandler = startHandler;
        _completeHandler = completeHandler;
        _validator = validator;
    }

    [HttpPost("start")]
    public async Task<IActionResult> Start([FromBody] StartRideRequest request)
    {
        var (isValid, error) = _validator.Validate(request);

        if (!isValid)
            return BadRequest(error);

        var command = new StartRideCommand(request.VehicleId, request.DriverId);
        var result = await _startHandler.Handle(command);

        if (!result.IsSuccess)
            return Conflict(result.Error);

        return Ok();
    }

    [HttpPost("{id}/complete")]
    public async Task<IActionResult> Complete(Guid id, [FromBody] CompleteRideRequest request)
    {
        var command = new CompleteRideCommand(id, request.EndLatitude, request.EndLongitude);
        var result = await _completeHandler.Handle(command);

        if (!result.IsSuccess)
            return Conflict(result.Error);

        return Ok();
    }
}