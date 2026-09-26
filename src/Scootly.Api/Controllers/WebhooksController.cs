using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Scootly.Api.Contracts.Requests;
using Scootly.Infrastructure.Messaging.Idempotency;
using Scootly.Infrastructure.Payments;
using Scootly.Infrastructure.Persistence;

namespace Scootly.Api.Controllers;

[ApiController]
[Route("api/webhooks")]
public sealed class WebhooksController : ControllerBase
{
    private readonly PaymentWebhookValidator _validator;
    private readonly ScootlyDbContext _dbContext;
    private readonly ILogger<WebhooksController> _logger;

    public WebhooksController(
        PaymentWebhookValidator validator,
        ScootlyDbContext dbContext,
        ILogger<WebhooksController> logger)
    {
        _validator = validator;
        _dbContext = dbContext;
        _logger = logger;
    }

    [HttpPost("payment-callback")]
    public async Task<IActionResult> PaymentCallback([FromBody] PaymentWebhookRequest request)
    {
        var payloadForSignature = JsonSerializer.Serialize(new
        {
            request.RideId,
            request.Success,
            request.Message
        });

        if (!_validator.IsValid(payloadForSignature, request.Signature))
        {
            _logger.LogWarning("Geçersiz webhook imzası: Ride={RideId}", request.RideId);
            return Unauthorized();
        }

        var webhookMessageId = DeterministicGuidFrom(request.RideId, request.Success);

        var idempotentHandler = new IdempotentMessageHandler(_dbContext);

        var processed = await idempotentHandler.TryProcessAsync(webhookMessageId, async () =>
        {
            _logger.LogInformation(
                "Ödeme webhook'u işlendi: Ride={RideId}, Success={Success}", request.RideId, request.Success);

            await Task.CompletedTask;
        });

        if (!processed)
        {
            _logger.LogInformation("Bu webhook bildirimi daha önce işlenmişti, atlandı: {RideId}", request.RideId);
        }

        return Ok();
    }

    private static Guid DeterministicGuidFrom(Guid rideId, bool success)
    {
        var input = $"{rideId}-{success}";
        var hash = System.Security.Cryptography.MD5.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        return new Guid(hash);
    }
}