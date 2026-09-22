using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Scootly.Api.Contracts.Requests;
using Scootly.Api.RateLimiting;
using Scootly.Application.Telemetry.Commands;
using Scootly.Infrastructure.Identity;

namespace Scootly.Api.Controllers;

/// <summary>Cihazların ölçüm gönderdiği uç (51. gün).</summary>
/// <remarks>
/// Yol <c>/api/v1/devices</c> önekinde olmak ZORUNDA:
/// <c>DeviceTokenScopeMiddleware</c> cihaz token'ını yalnızca bu önekteki
/// yollara bırakıyor. Başka bir yol seçilseydi cihaz kendi telemetri ucuna
/// 403 alırdı.
/// </remarks>
[ApiController]
[Route("api/v1/devices/telemetry")]
[Authorize]
[EnableRateLimiting(RateLimitPolicies.CihazTelemetri)]
public sealed class TelemetryController : ControllerBase
{
    private readonly IngestTelemetryBatchCommandHandler _handler;
    private readonly ILogger<TelemetryController> _logger;

    public TelemetryController(
        IngestTelemetryBatchCommandHandler handler,
        ILogger<TelemetryController> logger)
    {
        _handler = handler;
        _logger = logger;
    }

    /// <summary>Bir yığın ölçümü kabul eder ve kuyruğa koyar.</summary>
    /// <remarks>
    /// <para>
    /// <b>202 Accepted döner, 201 değil.</b> Fark önemli ve dürüst: bu uç
    /// yanıtı döndüğünde ölçümler henüz veritabanında DEĞİL, kuyrukta. 201
    /// "kaynak oluşturuldu" demek olurdu ve yalan söylerdi. 202 tam olarak
    /// "aldım, işleyeceğim" demek.
    /// </para>
    /// <para>
    /// Gövdedeki cihaz kimliği, token'daki kimlikle karşılaştırılıyor. Bu
    /// kontrol olmasaydı, geçerli bir cihaz token'ı olan herkes başka bir
    /// cihaz adına ölçüm uydurabilirdi — 26. günde <c>DriverId</c> için
    /// düzeltilen açığın telemetri hali.
    /// </para>
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(IngestSonucu), StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public IActionResult Ingest([FromBody] TelemetryBatchRequest request)
    {
        var tokenCihazi = User.FindFirstValue(ScootlyClaimTypes.DeviceId);

        if (string.IsNullOrEmpty(tokenCihazi))
        {
            return Forbid();
        }

        // Ordinal karşılaştırma: cihaz kimliği bir tanımlayıcı, metin değil.
        // Kültüre duyarlı karşılaştırma Türkçe yerelinde "I" ve "ı" harflerini
        // birbirine çevirir ve iki farklı cihaz aynı sayılabilirdi.
        if (request.Readings.Any(r => !string.Equals(r.DeviceId, tokenCihazi, StringComparison.Ordinal)))
        {
            _logger.LogWarning(
                "Cihaz {DeviceId} baska bir cihaz adina olcum gondermeye calisti.", tokenCihazi);

            return Forbid();
        }

        var sonuc = _handler.Handle(request.ToCommand());

        if (!sonuc.IsSuccess)
        {
            return BadRequest(sonuc.Error);
        }

        if (sonuc.Value is { Atilan: > 0 } deger)
        {
            // Atılan kayıt sessiz kalmıyor: hem logda hem yanıtta.
            _logger.LogWarning(
                "Kuyruk dolu: {Atilan} olcum atildi (cihaz {DeviceId}).", deger.Atilan, tokenCihazi);
        }

        return Accepted(sonuc.Value);
    }
}
