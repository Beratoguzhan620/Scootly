using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scootly.Api.Authorization;
using Scootly.Api.Contracts.Requests;
using Scootly.Api.Contracts.Responses;
using Scootly.Application.Abstractions;
using Scootly.Application.Common;
using Scootly.Application.Fleet.Commands;
using Scootly.Application.Riding.Commands;

namespace Scootly.Api.Controllers;

[ApiController]
// Sürümlü yol (30. gün). Eski sürümsüz yol da geçerli kalıyor: mevcut
// istemcileri bir anda kırmak yerine önce uyarmak, sonra kaldırmak doğru sıra.
// Kaldırma tarihi teknik borç listesinde.
[Route("api/vehicles")]
[Route("api/v1/vehicles")]
public sealed class VehiclesController : ControllerBase
{
    private readonly IApplicationDbContext _dbContext;
    private readonly ReserveVehicleCommandHandler _reserveHandler;
    private readonly RegisterVehicleCommandHandler _registerHandler;
    private readonly ICurrentUser _currentUser;

    public VehiclesController(
        IApplicationDbContext dbContext,
        ReserveVehicleCommandHandler reserveHandler,
        RegisterVehicleCommandHandler registerHandler,
        ICurrentUser currentUser)
    {
        _dbContext = dbContext;
        _reserveHandler = reserveHandler;
        _registerHandler = registerHandler;
        _currentUser = currentUser;
    }

    /// <summary>Araçları sayfalı olarak listeler. Ziyaretçiye de açık.</summary>
    /// <remarks>
    /// Sıralama (<c>OrderBy</c>) sayfalamanın vazgeçilmez parçası, süs değil.
    /// Sıralama verilmezse PostgreSQL satırları istediği düzende döndürebilir;
    /// aynı sorgu iki kez çalıştığında farklı sıra gelirse bazı kayıtlar iki
    /// sayfada birden çıkar, bazıları hiç çıkmaz — ve bu, veri az olduğu sürece
    /// fark edilmez.
    /// </remarks>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PagedResult<VehicleResponse>), StatusCodes.Status200OK)]
    public async Task<IActionResult> GetNearby(
        [FromQuery] PageRequest page,
        CancellationToken cancellationToken)
    {
        var sorgu = _dbContext.Vehicles.OrderBy(v => v.Id);

        var toplam = await sorgu.CountAsync(cancellationToken);

        var kayitlar = await sorgu
            .Skip((page.PageNumber - 1) * page.PageSize)
            .Take(page.PageSize)
            .Select(v => new VehicleResponse(
                v.Id,
                v.Location.Latitude,
                v.Location.Longitude,
                v.Battery.Percentage,
                v.Status.ToString()))
            .ToListAsync(cancellationToken);

        return Ok(new PagedResult<VehicleResponse>(kayitlar, page.PageNumber, page.PageSize, toplam));
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

        return Created($"/api/v1/vehicles/{result.Value}", new VehicleCreatedResponse(result.Value));
    }

    /// <summary>Aracı rezerve eder. Yalnızca sürücü rolü, yalnızca kendi adına.</summary>
    /// <remarks>
    /// 26. gündeki taramanın düzelttiği uç. Önceden <c>DriverId</c> istek
    /// gövdesinden geliyordu: sürücü A, gövdeye sürücü B'nin kimliğini yazarak
    /// B adına rezervasyon yapabiliyordu. Artık kimlik yalnızca token'dan
    /// okunuyor ve istemcinin onu etkilemesinin bir yolu yok.
    /// </remarks>
    [HttpPost("{id}/reserve")]
    [Authorize(Policy = PolicyNames.SadeceSurucu)]
    public async Task<IActionResult> Reserve(Guid id, CancellationToken cancellationToken)
    {
        var surucuId = _currentUser.UserId;

        if (surucuId == Guid.Empty)
        {
            // Politika buraya kimliksiz istek bırakmamalı; yine de kontrol var.
            // Güvenlik kontrolünün tek bir katmana bağlı olmaması istenen bir tekrardır.
            return Forbid();
        }

        var command = new ReserveVehicleCommand(id, surucuId);
        var result = await _reserveHandler.Handle(command, cancellationToken);

        if (!result.IsSuccess)
        {
            return Conflict(result.Error);
        }

        return Ok();
    }
}
