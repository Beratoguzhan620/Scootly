using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Scootly.Api.Contracts.Requests;
using Scootly.Api.ErrorHandling;
using Scootly.Api.Extensions;
using Scootly.Application.Payments.Commands;
using Scootly.Domain.Common;
using Scootly.Infrastructure.Messaging.Idempotency;
using Scootly.Infrastructure.Payments;

namespace Scootly.Api.Controllers;

/// <summary>
/// Ödeme sağlayıcısından gelen bildirimler. Kimlik doğrulaması JWT ile değil, ham gövde üzerinden
/// hesaplanan HMAC imzasıyla yapılır; aynı olay birden fazla gelirse yalnızca bir kez uygulanır.
/// </summary>
[ApiController]
[Route("api/webhooks")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.Webhook)]
public sealed class WebhooksController : ControllerBase
{
    public const string PaymentWebhookConsumer = "payment-webhook";

    /// <summary>Webhook gövdeleri küçüktür; büyük gövdeler imza hesabına bile alınmaz.</summary>
    private const int MaxBodyBytes = 16 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly PaymentWebhookValidator _validator;
    private readonly IdempotentMessageHandler _idempotentHandler;
    private readonly ApplyPaymentWebhookCommandHandler _handler;
    private readonly ILogger<WebhooksController> _logger;

    public WebhooksController(
        PaymentWebhookValidator validator,
        IdempotentMessageHandler idempotentHandler,
        ApplyPaymentWebhookCommandHandler handler,
        ILogger<WebhooksController> logger)
    {
        _validator = validator;
        _idempotentHandler = idempotentHandler;
        _handler = handler;
        _logger = logger;
    }

    [HttpPost("payment-callback")]
    [RequestSizeLimit(MaxBodyBytes)]
    [Consumes("application/json")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> PaymentCallback(CancellationToken cancellationToken)
    {
        // İmza, istemcinin gönderdiği baytlar üzerinden doğrulanmalı; yeniden serileştirilmiş JSON üzerinden değil.
        using var reader = new StreamReader(Request.Body, Encoding.UTF8);
        var rawBody = await reader.ReadToEndAsync(cancellationToken);

        if (!_validator.IsValid(rawBody, Request.Headers[PaymentWebhookValidator.SignatureHeaderName]))
        {
            _logger.LogWarning("Geçersiz veya süresi geçmiş webhook imzası reddedildi.");
            return Unauthorized();
        }

        PaymentWebhookRequest? request;

        try
        {
            request = JsonSerializer.Deserialize<PaymentWebhookRequest>(rawBody, JsonOptions);
        }
        catch (JsonException)
        {
            return this.BadRequestProblem("Webhook gövdesi ayrıştırılamadı.");
        }

        if (request is null || request.EventId == Guid.Empty || request.RideId == Guid.Empty)
            return this.BadRequestProblem("Webhook gövdesinde EventId ve RideId zorunludur.");

        Result? outcome = null;

        var processed = await _idempotentHandler.TryProcessAsync(
            request.EventId,
            PaymentWebhookConsumer,
            async token =>
            {
                outcome = await _handler.Handle(
                    new ApplyPaymentWebhookCommand(request.RideId, request.Success, request.Message ?? string.Empty),
                    token);

                // Çakışma gibi geçici hatalarda olay "işlendi" sayılmaz; sağlayıcının tekrar denemesi uygulanır.
                return outcome.IsSuccess || outcome.ErrorType == ErrorType.NotFound;
            },
            cancellationToken);

        if (!processed)
        {
            _logger.LogInformation("Webhook olayı daha önce işlenmişti: {EventId}", request.EventId);
            return Ok();
        }

        if (outcome is { IsSuccess: false })
        {
            _logger.LogWarning("Webhook uygulanamadı ({EventId}): {Error}", request.EventId, outcome.Error);

            // Bilinmeyen sürüş için sağlayıcının sonsuza kadar tekrar denemesinin anlamı yok.
            if (outcome.ErrorType == ErrorType.NotFound)
                return Ok();

            return this.ToProblem(outcome);
        }

        _logger.LogInformation("Ödeme webhook'u işlendi: Ride={RideId}, Success={Success}", request.RideId, request.Success);
        return Ok();
    }
}
