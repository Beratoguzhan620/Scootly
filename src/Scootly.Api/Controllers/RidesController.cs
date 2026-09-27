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
using Scootly.Application.Riding.Commands;
using Scootly.Domain.Riding;
using Scootly.Infrastructure.Caching;

namespace Scootly.Api.Controllers;

[ApiController]
[Route("api/rides")]
[Authorize(Policy = PolicyNames.DriverOnly)]
public sealed class RidesController : ControllerBase
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IAuthorizationService _authorizationService;
    private readonly ICurrentUser _currentUser;
    private readonly NearbyVehicleCache _cache;

    public RidesController(
        IApplicationDbContext dbContext,
        IAuthorizationService authorizationService,
        ICurrentUser currentUser,
        NearbyVehicleCache cache)
    {
        _dbContext = dbContext;
        _authorizationService = authorizationService;
        _currentUser = currentUser;
        _cache = cache;
    }

    [HttpGet("{id:guid}", Name = nameof(GetById))]
    [ProducesResponseType<RideResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
    {
        var ride = await FindOwnedRideAsync(id, cancellationToken);

        return ride is null ? NotFound() : Ok(ToResponse(ride));
    }

    [HttpGet("active")]
    [ProducesResponseType<RideResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetActive(CancellationToken cancellationToken)
    {
        var driverId = _currentUser.UserId;

        var ride = await _dbContext.Rides
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.DriverId == driverId && r.Status == RideStatus.Active, cancellationToken);

        return ride is null ? NotFound() : Ok(ToResponse(ride));
    }

    [HttpGet("{id:guid}/payment-status")]
    [ProducesResponseType<PaymentStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetPaymentStatus(Guid id, CancellationToken cancellationToken)
    {
        var ride = await FindOwnedRideAsync(id, cancellationToken);

        if (ride is null)
            return NotFound();

        return Ok(new PaymentStatusResponse(ride.Id, ToPaymentStatusText(ride.PaymentStatus), ride.Fare));
    }

    [HttpPost("start")]
    [EnableRateLimiting(RateLimitPolicies.User)]
    [ProducesResponseType<StartRideResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Start(
        [FromBody] StartRideRequest request,
        [FromServices] StartRideRequestValidator validator,
        [FromServices] StartRideCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var validation = validator.Validate(request);

        if (!validation.IsValid)
            return this.BadRequestProblem(validation.Error!);

        var result = await handler.Handle(new StartRideCommand(request.VehicleId, _currentUser.UserId), cancellationToken);

        if (!result.IsSuccess)
            return this.ToProblem(result);

        await _cache.InvalidateAsync(CacheKeys.NearbyVehiclesDefault(), cancellationToken);

        return CreatedAtRoute(nameof(GetById), new { id = result.Value }, new StartRideResponse(result.Value));
    }

    [HttpPost("{id:guid}/complete")]
    [EnableRateLimiting(RateLimitPolicies.User)]
    [ProducesResponseType<CompleteRideResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Complete(
        Guid id,
        [FromBody] CompleteRideRequest request,
        [FromServices] CompleteRideRequestValidator validator,
        [FromServices] CompleteRideCommandHandler handler,
        CancellationToken cancellationToken)
    {
        var validation = validator.Validate(request);

        if (!validation.IsValid)
            return this.BadRequestProblem(validation.Error!);

        var result = await handler.Handle(
            new CompleteRideCommand(id, _currentUser.UserId, request.EndLatitude, request.EndLongitude),
            cancellationToken);

        if (!result.IsSuccess)
            return this.ToProblem(result);

        await _cache.InvalidateAsync(CacheKeys.NearbyVehiclesDefault(), cancellationToken);

        var completed = result.Value!;

        // Ödeme asenkron işlenir (outbox → tüketici); istemci durumu payment-status ucundan izler.
        return Accepted(
            Url.Link(nameof(GetById), new { id = completed.RideId }),
            new CompleteRideResponse(
                completed.RideId,
                RideStatus.Completed.ToString(),
                ToPaymentStatusText(completed.PaymentStatus),
                completed.Fare));
    }

    /// <summary>Sürüşü yükler ve sahiplik politikasını uygular; başkasına ait sürüşün varlığı açığa çıkarılmaz.</summary>
    private async Task<Ride?> FindOwnedRideAsync(Guid id, CancellationToken cancellationToken)
    {
        var ride = await _dbContext.Rides.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

        if (ride is null)
            return null;

        var authorization = await _authorizationService.AuthorizeAsync(User, ride, PolicyNames.RideOwner);

        return authorization.Succeeded ? ride : null;
    }

    private static RideResponse ToResponse(Ride ride) => new(
        ride.Id,
        ride.DriverId,
        ride.VehicleId,
        ride.Status.ToString(),
        ride.StartedAt,
        ride.EndedAt,
        ride.Fare,
        ToPaymentStatusText(ride.PaymentStatus));

    private static string ToPaymentStatusText(PaymentStatus status) => status switch
    {
        PaymentStatus.None => "NotYetCharged",
        _ => status.ToString()
    };
}
