using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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
    private readonly StartRideRequestValidator _validator;
    private readonly IAuthorizationService _authorizationService;
    private readonly ICurrentUser _currentUser;

    public RidesController(
        IApplicationDbContext dbContext,
        StartRideCommandHandler startHandler,
        CompleteRideCommandHandler completeHandler,
        StartRideRequestValidator validator,
        IAuthorizationService authorizationService,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _startHandler = startHandler;
        _completeHandler = completeHandler;
        _validator = validator;
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

        var ride = _dbContext.Rides.FirstOrDefault(r => r.Id == id);

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

    [HttpPost("start")]
    [Authorize]
    public async Task<IActionResult> Start([FromBody] StartRideRequest request)
    {
        var (isValid, error) = _validator.Validate(request);

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
        var command = new CompleteRideCommand(id, request.EndLatitude, request.EndLongitude);
        var result = await _completeHandler.Handle(command);

        if (!result.IsSuccess)
            return Conflict(result.Error);

        return Ok();
    }
}