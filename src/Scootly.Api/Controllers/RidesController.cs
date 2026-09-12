using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scootly.Api.Contracts.Requests;
using Scootly.Api.Validators;
using Scootly.Application.Riding.Commands;

namespace Scootly.Api.Controllers;

[ApiController]
[Route("api/rides")]
// Sürüş uçlarının tamamı giriş yapmış kullanıcı ister.
// Varsayılan politika bunu zaten sağlıyor; burada AÇIKÇA yazılmasının nedeni,
// varsayılanı ileride biri değiştirirse bu controller'ın korumasız kalmaması.
// Güvenlik kontrolünün tek bir yere bağlı olmaması istenen bir tekrardır.
[Authorize]
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

    /// <summary>Sürüş başlatır.</summary>
    /// <remarks>
    /// <c>Reserve</c> ile aynı eksik: <c>DriverId</c> gövdeden geliyor.
    /// 24. ve 26. günlerde kapatılacak.
    /// </remarks>
    [HttpPost("start")]
    public async Task<IActionResult> Start([FromBody] StartRideRequest request)
    {
        var (isValid, error) = _validator.Validate(request);

        if (!isValid)
        {
            return BadRequest(error);
        }

        var command = new StartRideCommand(request.VehicleId, request.DriverId);
        var result = await _startHandler.Handle(command);

        if (!result.IsSuccess)
        {
            return Conflict(result.Error);
        }

        return Ok();
    }

    /// <summary>Sürüşü bitirir.</summary>
    /// <remarks>
    /// Bu uçta sahiplik kontrolü HİÇ YOK: giriş yapmış herhangi bir kullanıcı,
    /// URL'deki kimliği değiştirerek başkasının sürüşünü bitirebilir.
    /// Üçünün içinde en ağırı bu; 24. günün asıl hedefi.
    /// </remarks>
    [HttpPost("{id}/complete")]
    public async Task<IActionResult> Complete(Guid id, [FromBody] CompleteRideRequest request)
    {
        var command = new CompleteRideCommand(id, request.EndLatitude, request.EndLongitude);
        var result = await _completeHandler.Handle(command);

        if (!result.IsSuccess)
        {
            return Conflict(result.Error);
        }

        return Ok();
    }
}
