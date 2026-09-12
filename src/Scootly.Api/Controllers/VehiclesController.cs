using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scootly.Api.Authorization;
using Scootly.Api.Contracts.Requests;
using Scootly.Api.Contracts.Responses;
using Scootly.Application.Abstractions;
using Scootly.Application.Fleet.Commands;
using Scootly.Application.Riding.Commands;

namespace Scootly.Api.Controllers;

[ApiController]
[Route("api/vehicles")]
public sealed class VehiclesController : ControllerBase
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ReserveVehicleCommandHandler _reserveHandler;
    private readonly RegisterVehicleCommandHandler _registerHandler;

    public VehiclesController(
        IApplicationDbContext dbContext,
        ReserveVehicleCommandHandler reserveHandler,
        RegisterVehicleCommandHandler registerHandler)
    {
        _dbContext = dbContext;
        _reserveHandler = reserveHandler;
        _registerHandler = registerHandler;
    }

    /// <summary>Yakındaki araçları listeler. Ziyaretçiye de açık.</summary>
    /// <remarks>
    /// Varsayılan politika kimlik doğrulaması istiyor; bu uç ondan bilinçli
    /// olarak muaf tutuldu. Plandaki altı aktörden biri "Ziyaretçi": henüz kaydı
    /// olmayan biri, kaydolmaya değip değmeyeceğine yakınında araç olup
    /// olmadığına bakarak karar verir. Muafiyetin AÇIKÇA yazılmış olması önemli —
    /// gözden geçirmede görünen bir satır.
    /// </remarks>
    [HttpGet]
    [AllowAnonymous]
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

    /// <summary>Filoya yeni araç ekler. Yalnızca filo yöneticisi.</summary>
    [HttpPost]
    [Authorize(Policy = PolicyNames.SadeceYonetici)]
    [ProducesResponseType(typeof(VehicleCreatedResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterVehicleRequest request,
        CancellationToken cancellationToken)
    {
        var command = new RegisterVehicleCommand(
            request.Brand,
            request.RangeKm,
            request.Latitude,
            request.Longitude,
            request.BatteryPercentage);

        var result = await _registerHandler.Handle(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return Conflict(result.Error);
        }

        return Created($"/api/vehicles/{result.Value}", new VehicleCreatedResponse(result.Value));
    }

    /// <summary>Aracı rezerve eder. Yalnızca sürücü rolü.</summary>
    /// <remarks>
    /// DİKKAT — bu uç bugün itibariyle EKSİK korunuyor. Rol kontrolü "bu bir
    /// sürücü mü" sorusuna cevap veriyor ama "bu sürücü BAŞKASININ adına işlem
    /// yapıyor mu" sorusuna cevap vermiyor: <c>DriverId</c> hâlâ istek
    /// gövdesinden geliyor, token'dan değil. Yani sürücü A, gövdeye sürücü B'nin
    /// kimliğini yazarak B adına rezervasyon yapabilir.
    /// Bu, rol tabanlı yetkinin nerede yetersiz kaldığının somut örneği;
    /// 24. günde kaynak tabanlı yetkiyle, 26. gündeki OWASP taramasında da
    /// kimliğin token'dan okunmasıyla kapatılacak. Teknik borç listesinde kayıtlı.
    /// </remarks>
    [HttpPost("{id}/reserve")]
    [Authorize(Policy = PolicyNames.SadeceSurucu)]
    public async Task<IActionResult> Reserve(Guid id, [FromBody] ReserveVehicleRequest request)
    {
        var command = new ReserveVehicleCommand(id, request.DriverId);
        var result = await _reserveHandler.Handle(command);

        if (!result.IsSuccess)
        {
            return Conflict(result.Error);
        }

        return Ok();
    }
}
