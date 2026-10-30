using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Scootly.Api.Contracts.Responses;
using Scootly.Application.Abstractions;
using Scootly.Domain.Fleet;
using Scootly.Domain.Riding;
using Scootly.Infrastructure.Authorization;

namespace Scootly.Api.Controllers;

/// <summary>
/// Sürücünün rezervasyonu. Rezervasyon ayrı bir kayıt değil, aracın durumudur (<c>Vehicle.ReservedBy/ReservedAt</c>);
/// bu uç, mobil uygulama kapatılıp açıldığında ya da sürücü başka bir cihazdan girdiğinde süren rezervasyonu geri getirir.
/// </summary>
[ApiController]
[Route("api/reservations")]
[Authorize(Policy = PolicyNames.DriverOnly)]
public sealed class ReservationsController : ControllerBase
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IVehicleReadService _readService;
    private readonly ICurrentUser _currentUser;
    private readonly IClock _clock;

    public ReservationsController(
        IApplicationDbContext dbContext,
        IVehicleReadService readService,
        ICurrentUser currentUser,
        IClock clock)
    {
        _dbContext = dbContext;
        _readService = readService;
        _currentUser = currentUser;
        _clock = clock;
    }

    /// <summary>
    /// Sürücünün süren rezervasyonu. Rezervasyon yoksa (iptal edilmiş, süresi dolmuş ya da sürüşe dönüşmüş) 204 döner;
    /// süresi dolmuş ama arka plan işinin henüz serbest bırakmadığı rezervasyon da yok sayılır.
    /// </summary>
    [HttpGet("active")]
    [ProducesResponseType<ActiveReservationResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> GetActive(CancellationToken cancellationToken)
    {
        var driverId = _currentUser.UserId;

        var reservation = await _dbContext.Vehicles
            .AsNoTracking()
            .Where(v => v.ReservedBy == driverId && v.Status == VehicleStatus.Reserved && v.ReservedAt != null)
            .OrderByDescending(v => v.ReservedAt)
            .Select(v => new { v.Id, v.ReservedAt })
            .FirstOrDefaultAsync(cancellationToken);

        if (reservation is null || reservation.ReservedAt is null)
            return NoContent();

        var reservedAt = DateTime.SpecifyKind(reservation.ReservedAt.Value, DateTimeKind.Utc);

        if (ReservationPolicy.IsExpired(reservedAt, _clock.UtcNow))
            return NoContent();

        var vehicle = await _readService.GetByIdAsync(reservation.Id, cancellationToken);

        if (vehicle is null)
            return NoContent();

        return Ok(new ActiveReservationResponse(
            vehicle.Id,
            reservedAt,
            reservedAt + ReservationPolicy.Duration,
            new VehicleResponseV2(vehicle.Id, vehicle.Latitude, vehicle.Longitude, vehicle.BatteryPercentage, vehicle.Status, vehicle.Brand, vehicle.RangeKm)));
    }
}
