using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scootly.Api.Authorization;
using Scootly.Api.Contracts.Requests;
using Scootly.Api.Contracts.Responses;
using Scootly.Application.Abstractions;
using Scootly.Application.Common;
using Scootly.Application.Fleet.Commands;
using Scootly.Application.Fleet.Queries;
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
    private readonly FindNearbyVehiclesQueryHandler _nearbyHandler;
    private readonly ReserveVehicleCommandHandler _reserveHandler;
    private readonly RegisterVehicleCommandHandler _registerHandler;
    private readonly ICurrentUser _currentUser;

    public VehiclesController(
        FindNearbyVehiclesQueryHandler nearbyHandler,
        ReserveVehicleCommandHandler reserveHandler,
        RegisterVehicleCommandHandler registerHandler,
        ICurrentUser currentUser)
    {
        _nearbyHandler = nearbyHandler;
        _reserveHandler = reserveHandler;
        _registerHandler = registerHandler;
        _currentUser = currentUser;
    }

    /// <summary>Verilen noktanın çevresindeki müsait araçlar. Ziyaretçiye açık.</summary>
    /// <remarks>
    /// <para>
    /// <b>42. günde kırılan sözleşme.</b> Bu uç bugüne kadar "yakındaki
    /// araçlar" adını taşıyordu ama hiçbir konum filtresi yoktu: tablodaki her
    /// aracı sayfalayarak döndürüyordu. Artık enlem ve boylam ZORUNLU.
    /// </para>
    /// <para>
    /// Kırıcı bir değişikliği sürümlemek yerine yapmayı seçtik çünkü bu ucun
    /// henüz tek bir istemcisi yok (MVC paneli 17. haftada, mobil hiç yok).
    /// Yanlış davranışı bir sürüm numarasının arkasında dondurmak, ileride onu
    /// desteklemeye devam etmek demekti. Karar teknik borç listesinde kayıtlı.
    /// </para>
    /// <para>
    /// Sorgu artık controller'da değil, Application katmanındaki bir handler'da
    /// (41-43. günler): takipsiz okuma, dört alanlı projeksiyon ve veritabanı
    /// tarafında yarıçap filtresi orada.
    /// </para>
    /// </remarks>
    [HttpGet]
    [AllowAnonymous]
    [ProducesResponseType(typeof(PagedResult<NearbyVehicleResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> GetNearby(
        [FromQuery] NearbyQueryRequest request,
        CancellationToken cancellationToken)
    {
        var sonuc = await _nearbyHandler.Handle(request.ToQuery(), cancellationToken);

        var kayitlar = sonuc.Items
            .Select(v => new NearbyVehicleResponse(v.Id, v.Latitude, v.Longitude, v.BatteryPercentage))
            .ToList();

        return Ok(new PagedResult<NearbyVehicleResponse>(
            kayitlar, sonuc.PageNumber, sonuc.PageSize, sonuc.TotalCount));
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
