using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scootly.Api.Authorization;
using Scootly.Api.Contracts.Requests;
using Scootly.Api.Contracts.Responses;
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
    private readonly StartRideRequestValidator _startValidator;
    private readonly CompleteRideRequestValidator _completeValidator;
    private readonly IRideRepository _rides;
    private readonly IAuthorizationService _authorization;
    private readonly ICurrentUser _currentUser;

    public RidesController(
        StartRideCommandHandler startHandler,
        CompleteRideCommandHandler completeHandler,
        StartRideRequestValidator startValidator,
        CompleteRideRequestValidator completeValidator,
        IRideRepository rides,
        IAuthorizationService authorization,
        ICurrentUser currentUser)
    {
        _startHandler = startHandler;
        _completeHandler = completeHandler;
        _startValidator = startValidator;
        _completeValidator = completeValidator;
        _rides = rides;
        _authorization = authorization;
        _currentUser = currentUser;
    }

    /// <summary>Tek bir sürüşü getirir. Yalnızca sürüşün sahibi.</summary>
    /// <remarks>
    /// Okuma ucu da yazma ucu kadar sızdırır: kimlik numarası tahmin edilebilir
    /// olmasa bile, sahiplik kontrolü olmadan eline bir kimlik geçen herkes
    /// başkasının sürüşünü, konumlarını ve ücretini okuyabilirdi. OWASP A01 —
    /// yetkisiz nesne erişimi — burada da geçerli.
    ///
    /// Bulunmayan sürüş ile başkasına ait sürüş AYNI yanıtı vermiyor (404 ve
    /// 403). Burada bu bilinçli: kimlik rastgele bir GUID, tahmin edilerek
    /// taranamaz, dolayısıyla 403 ile 404 arasındaki fark saldırgana işe yarar
    /// bir bilgi vermiyor. Giriş ucunda ise durum tersiydi — orada e-posta
    /// tahmin edilebilir olduğu için iki durum aynı yanıtı veriyor.
    /// </remarks>
    [HttpGet("{id}")]
    [ProducesResponseType(typeof(RideResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken)
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

        return Ok(new RideResponse(
            ride.Id,
            ride.VehicleId,
            ride.StartedAt,
            ride.EndedAt,
            ride.Status.ToString(),
            ride.Fare));
    }

    /// <summary>Sürüş başlatır. Sürücü kimliği token'dan okunur.</summary>
    /// <remarks>
    /// 26. gündeki taramanın düzelttiği uç. <c>DriverId</c> gövdeden kaldırıldı;
    /// istemcinin kimliği etkilemesinin bir yolu kalmadı.
    /// </remarks>
    [HttpPost("start")]
    [Authorize(Policy = PolicyNames.SadeceSurucu)]
    public async Task<IActionResult> Start(
        [FromBody] StartRideRequest request,
        CancellationToken cancellationToken)
    {
        var (isValid, error) = _startValidator.Validate(request);

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
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Complete(
        Guid id,
        [FromBody] CompleteRideRequest request,
        CancellationToken cancellationToken)
    {
        var (isValid, error) = _completeValidator.Validate(request);

        if (!isValid)
        {
            return BadRequest(error);
        }

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
