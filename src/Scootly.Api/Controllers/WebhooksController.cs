using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scootly.Application.Abstractions;
using Scootly.Application.Behaviors;
using Scootly.Application.IntegrationEvents;
using Scootly.Domain.Common;
using Scootly.Infrastructure.Payments;

namespace Scootly.Api.Controllers;

/// <summary>Dış sistemlerden gelen bildirimler (70. gün).</summary>
/// <remarks>
/// <para>
/// Webhook kuyruğun tersi yön: dış dünya bize HTTP ile "bir şey oldu" diyor.
/// Uç kimlik doğrulaması istemiyor (ödeme sağlayıcısının bir kullanıcı
/// token'ı yok); onun yerine her istek <b>imzalı</b>. İmza tutmazsa 401.
/// </para>
/// <para>
/// <b>Bildirim doğrudan işlenmiyor, outbox'a yazılıyor.</b> Ödeme sonucunu
/// burada işlemek (aracı serbest bırakmak, borç açmak) kuyruktan gelen sonucu
/// işleyen tüketicinin mantığını ikinci kez yazmak olurdu. Webhook yalnızca
/// aynı <c>payment.authorized</c> olayını üretiyor; gerisi saga'nın son
/// adımında, tek yerde.
/// </para>
/// <para>
/// <b>Tekrar gelen bildirim 200 alıyor, 409 değil.</b> Sağlayıcılar 2xx
/// görene kadar bildirimi yeniden gönderir; ikinci gelişe hata dönmek onu
/// sonsuz bir yeniden deneme döngüsüne sokardı.
/// </para>
/// </remarks>
[ApiController]
[Route("api/webhooks")]
[AllowAnonymous]
public sealed class WebhooksController : ControllerBase
{
    public const string TimestampHeader = "X-Scootly-Timestamp";
    public const string SignatureHeader = "X-Scootly-Signature";
    private const string Tuketici = "webhook:payments";
    private const int AzamiGovde = 64 * 1024;

    private readonly PaymentWebhookValidator _validator;
    private readonly IdempotencyBehavior _idempotency;
    private readonly IOutboxWriter _outbox;
    private readonly ILogger<WebhooksController> _logger;

    public WebhooksController(
        PaymentWebhookValidator validator,
        IdempotencyBehavior idempotency,
        IOutboxWriter outbox,
        ILogger<WebhooksController> logger)
    {
        _validator = validator;
        _idempotency = idempotency;
        _outbox = outbox;
        _logger = logger;
    }

    [HttpPost("payments")]
    [RequestSizeLimit(AzamiGovde)]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> PaymentCallback(CancellationToken cancellationToken)
    {
        // Imza HAM govde uzerinden hesaplaniyor; model baglama (FromBody)
        // govdeyi once JSON olarak yorumlardi ve yeniden serilestirilen metin
        // bayt bayt ayni olmayabilirdi. Once ham baytlar, sonra JSON.
        byte[] govde;
        using (var bellek = new MemoryStream())
        {
            await Request.Body.CopyToAsync(bellek, cancellationToken);
            govde = bellek.ToArray();
        }

        if (!_validator.IsConfigured)
        {
            _logger.LogError("Payments:WebhookSecret tanimli degil; odeme webhook'u reddedildi.");
            return Unauthorized();
        }

        if (!_validator.IsValid(
                Request.Headers[TimestampHeader].ToString(),
                Request.Headers[SignatureHeader].ToString(),
                govde))
        {
            _logger.LogWarning("Gecersiz webhook imzasi. Kaynak: {Ip}", HttpContext.Connection.RemoteIpAddress);
            return Unauthorized();
        }

        PaymentWebhookPayload? bildirim;
        try
        {
            bildirim = JsonSerializer.Deserialize<PaymentWebhookPayload>(govde, JsonSerializerOptions.Web);
        }
        catch (JsonException)
        {
            return BadRequest("Gecersiz JSON.");
        }

        if (bildirim is null || bildirim.EventId == Guid.Empty || bildirim.RideId == Guid.Empty || bildirim.Amount < 0)
            return BadRequest("Eksik ya da gecersiz alan.");

        var cikti = await _idempotency.ExecuteAsync(
            bildirim.EventId,
            Tuketici,
            async ct =>
            {
                await _outbox.WriteAsync(
                    new PaymentAuthorizedIntegrationEvent(
                        EventId: bildirim.EventId,
                        OccurredOnUtc: DateTime.UtcNow,
                        RideId: bildirim.RideId,
                        Amount: bildirim.Amount,
                        Success: bildirim.Success,
                        FailureReason: bildirim.Success ? null : bildirim.FailureReason),
                    ct);

                return Result.Success();
            },
            cancellationToken);

        if (cikti.WasDuplicate)
            _logger.LogInformation("Odeme webhook'u tekrar geldi, atlandi: {EventId}", bildirim.EventId);

        return Ok();
    }

    /// <summary>Ödeme sağlayıcısının gönderdiği gövde.</summary>
    public sealed record PaymentWebhookPayload(
        Guid EventId,
        Guid RideId,
        decimal Amount,
        bool Success,
        string? FailureReason);
}
