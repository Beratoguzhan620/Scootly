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
[Route("api/v1/rides")]
[Authorize]
public sealed class RidesController : ControllerBase
{
    private readonly StartRideCommandHandler _startHandler;
    private readonly CompleteRideCommandHandler _completeHandler;
    private readonly StartRideRequestValidator _validator;
    private readonly IRideRepository _rides;
    private readonly IAuthorizationService _authorization;
    private readonly ICurrentUser _currentUser;

    public RidesController(
        StartRideCommandHandler startHandler,
        CompleteRideCommandHandler completeHandler,
        StartRideRequestValidator validator,
        IRideRepository rides,
        IAuthorizationService authorization,
        ICurrentUser currentUser)
    {
        _startHandler = startHandler;
        _completeHandler = completeHandler;
        _validator = validator;
        _rides = rides;
        _authorization = authorization;
        _currentUser = currentUser;
    }

    /// <summary>Sürüş başlatır. Sürücü kimliği token'dan okunur.</summary>
    /// <remarks>
    /// 26. gündeki taramanın düzelttiği ikinci uç. <c>DriverId</c> gövdeden
    /// kaldırıldı; istemcinin kimliği etkilemesinin bir yolu kalmadı.
    /// </remarks>
    [HttpPost("start")]
    [Authorize(Policy = PolicyNames.SadeceSurucu)]
    public async Task<IActionResult> Start(
        [FromBody] StartRideRequest request,
        CancellationToken cancellationToken)
    {
        var (isValid, error) = _validator.Validate(request);

        if (!isValid)
        {
            return BadRequest(error);
        }

        var surucuId = _currentUser.UserId;

        if (surucuId == Guid.Empty)
        {
            return Forbid();
        }

        var command = new StartRideCommand(request.VehicleId, surucuId);
        var result = await _startHandler.Handle(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return Conflict(result.Error);
        }

        return Ok();
    }

    /// <summary>Sürüşü bitirir. Yalnızca sürüşün sahibi.</summary>
    /// <remarks>
    /// Kaynak tabanlı yetkilendirme (24. gün): karar öznitelikle verilemez,
    /// çünkü öznitelik istek hiçbir şey okumadan çalışır ve elinde sürüş yoktur.
    /// </remarks>
    [HttpPost("{id}/complete")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Complete(
        Guid id,
        [FromBody] CompleteRideRequest request,
        CancellationToken cancellationToken)
    {
        var ride = await _rides.GetByIdAsync(id, cancellationToken);

        if (ride is null)
        {
            return NotFound();
        }

        var yetki = await _authorization.AuthorizeAsync(User, ride, PolicyNames.SurusSahibi);

        if (!yetki.Succeeded)
        {
            return Forbid();
        }

        var command = new CompleteRideCommand(id, request.EndLatitude, request.EndLongitude);
        var result = await _completeHandler.Handle(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return Conflict(result.Error);
        }

        return Ok();
    }
}
