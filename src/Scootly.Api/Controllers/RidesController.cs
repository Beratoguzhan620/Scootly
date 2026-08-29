using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Scootly.Api.Contracts.Requests;
using Scootly.Api.Contracts.Responses;
using Scootly.Application.Riding.Commands;
using Scootly.Domain.Geo;

namespace Scootly.Api.Controllers;

[ApiController]
[Route("api/rides")]
public sealed class RidesController : ControllerBase
{
    private readonly StartRideCommandHandler _startHandler;
    private readonly CompleteRideCommandHandler _completeHandler;
    private readonly IValidator<StartRideRequest> _startValidator;
    private readonly IValidator<CompleteRideRequest> _completeValidator;

    public RidesController(
        StartRideCommandHandler startHandler,
        CompleteRideCommandHandler completeHandler,
        IValidator<StartRideRequest> startValidator,
        IValidator<CompleteRideRequest> completeValidator)
    {
        _startHandler = startHandler;
        _completeHandler = completeHandler;
        _startValidator = startValidator;
        _completeValidator = completeValidator;
    }

    [HttpPost]
    public async Task<IActionResult> Start(
        [FromBody] StartRideRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await _startValidator.ValidateAsync(request, cancellationToken);

        if (!validation.IsValid)
            return BadRequest(ToErrorResponse(validation));

        var result = await _startHandler.Handle(
            new StartRideCommand(request.VehicleId, request.DriverId),
            cancellationToken);

        if (!result.IsSuccess)
            return NotFound(new ApiErrorResponse("Bulunamadı", result.Error, StatusCodes.Status404NotFound));

        return Ok(new { rideId = result.Value });
    }

    [HttpPost("{id:guid}/complete")]
    public async Task<IActionResult> Complete(
        Guid id,
        [FromBody] CompleteRideRequest request,
        CancellationToken cancellationToken)
    {
        var validation = await _completeValidator.ValidateAsync(request, cancellationToken);

        if (!validation.IsValid)
            return BadRequest(ToErrorResponse(validation));

        var result = await _completeHandler.Handle(
            new CompleteRideCommand(id, new GeoPoint(request.EndLatitude, request.EndLongitude)),
            cancellationToken);

        if (!result.IsSuccess)
            return NotFound(new ApiErrorResponse("Bulunamadı", result.Error, StatusCodes.Status404NotFound));

        return NoContent();
    }

    private static ApiErrorResponse ToErrorResponse(FluentValidation.Results.ValidationResult validation)
    {
        var detail = string.Join(" ", validation.Errors.Select(e => e.ErrorMessage));

        return new ApiErrorResponse("Doğrulama hatası", detail, StatusCodes.Status400BadRequest);
    }
}