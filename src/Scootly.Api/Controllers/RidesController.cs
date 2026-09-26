using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scootly.Api.Authorization;
using Scootly.Api.Contracts.Requests;
using Scootly.Api.Validators;
using Scootly.Application.Abstractions;
using Scootly.Application.Riding.Commands;

namespace Scootly.Api.Controllers;

[ApiController]
[Route("api/rides")]
public sealed class RidesController : ControllerBase
{
    private readonly IApplicationDbContext _dbContext;
    private readonly StartRideCommandHandler _startHandler;
    private readonly CompleteRideCommandHandler _completeHandler;
    private readonly StartRideRequestValidator _startValidator;
    private readonly CompleteRideRequestValidator _completeValidator;
    private readonly IAuthorizationService _authorizationService;
    private readonly ICurrentUser _currentUser;

    public RidesController(
        IApplicationDbContext dbContext,
        StartRideCommandHandler startHandler,
        CompleteRideCommandHandler completeHandler,
        StartRideRequestValidator startValidator,
        CompleteRideRequestValidator completeValidator,
        IAuthorizationService authorizationService,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _startHandler = startHandler;
        _completeHandler = completeHandler;
        _startValidator = startValidator;
        _completeValidator = completeValidator;
        _authorizationService = authorizationService;
        _currentUser = currentUser;
    }

    [HttpGet("{id}")]
    [Authorize]
    public async Task<IActionResult> GetById(Guid id)
    {
        var authResult = await _authorizationService.AuthorizeAsync(User, id, PolicyNames.RideOwner);

        if (!authResult.Succeeded)
            return Forbid();

        var ride = _dbContext.Rides.AsNoTracking().FirstOrDefault(r => r.Id == id);

        if (ride is null)
            return NotFound();

        return Ok(new
        {
            ride.Id,
            ride.DriverId,
            ride.VehicleId,
            Status = ride.Status.ToString()
        });
    }

    [HttpGet("{id}/payment-status")]
    [Authorize]
    public async Task<IActionResult> GetPaymentStatus(Guid id)
    {
        var authResult = await _authorizationService.AuthorizeAsync(User, id, PolicyNames.RideOwner);

        if (!authResult.Succeeded)
            return Forbid();

        var ride = _dbContext.Rides.AsNoTracking().FirstOrDefault(r => r.Id == id);

        if (ride is null)
            return NotFound();

        var paymentStatus = ride.Status.ToString() switch
        {
            "Active" or "Reserved" => "NotYetCharged",
            "Completed" when ride.Fare.HasValue => "Paid",
            "Completed" => "Pending",
            "PaymentPending" => "Pending",
            "Abandoned" => "NotApplicable",
            _ => "Unknown"
        };

        return Ok(new
        {
            RideId = ride.Id,
            PaymentStatus = paymentStatus,
            Fare = ride.Fare
        });
    }

    [HttpPost("start")]
    [Authorize]
    public async Task<IActionResult> Start([FromBody] StartRideRequest request)
    {
        var (isValid, error) = _startValidator.Validate(request);

        if (!isValid)
            return BadRequest(error);

        var command = new StartRideCommand(request.VehicleId, _currentUser.UserId);
        var result = await _startHandler.Handle(command);

        if (!result.IsSuccess)
            return Conflict(result.Error);

        return Ok();
    }

    [HttpPost("{id}/complete")]
    [Authorize]
    public async Task<IActionResult> Complete(Guid id, [FromBody] CompleteRideRequest request)
    {
        var (isValid, error) = _completeValidator.Validate(request);

        if (!isValid)
            return BadRequest(error);

        var command = new CompleteRideCommand(id, request.EndLatitude, request.EndLongitude);
        var result = await _completeHandler.Handle(command);

        if (!result.IsSuccess)
            return Conflict(result.Error);

        return Ok(new { Status = "Completed", PaymentStatus = "Processing" });
    }
}