using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Scootly.Api.Authorization;
using Scootly.Api.Contracts.Requests;
using Scootly.Api.Contracts.Responses;
using Scootly.Api.ErrorHandling;
using Scootly.Api.Extensions;
using Scootly.Api.Validators;
using Scootly.Application.Abstractions;
using Scootly.Application.Telemetry;

namespace Scootly.Api.Controllers;

[ApiController]
[Route("api/telemetry")]
[Authorize(Policy = PolicyNames.DeviceOnly)]
[EnableRateLimiting(RateLimitPolicies.Device)]
public sealed class TelemetryController : ControllerBase
{
    private const int RetryAfterSeconds = 5;

    private readonly TelemetryChannel _telemetryChannel;
    private readonly IVehicleRepository _vehicleRepository;
    private readonly TelemetryBatchRequestValidator _validator;
    private readonly IClock _clock;

    public TelemetryController(
        TelemetryChannel telemetryChannel,
        IVehicleRepository vehicleRepository,
        TelemetryBatchRequestValidator validator,
        IClock clock)
    {
        _telemetryChannel = telemetryChannel;
        _vehicleRepository = vehicleRepository;
        _validator = validator;
        _clock = clock;
    }

    /// <summary>
    /// Okumaları doğrular ve işlenmek üzere kuyruğa alır (202). Kayıtlı olmayan araçlara ait okumalar reddedilir;
    /// kuyruk doluysa hiçbir okuma alınmaz ve istemciye 503 + Retry-After döner.
    /// </summary>
    [HttpPost("batch")]
    [ProducesResponseType<TelemetryBatchResponse>(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> IngestBatch([FromBody] TelemetryBatchRequest request, CancellationToken cancellationToken)
    {
        var validation = _validator.Validate(request);

        if (!validation.IsValid)
            return this.BadRequestProblem(validation.Error!);

        var vehicleIds = request.Readings.Select(r => r.VehicleId).Distinct().ToList();
        var knownVehicleIds = await _vehicleRepository.GetExistingIdsAsync(vehicleIds, cancellationToken);
        var receivedAt = _clock.UtcNow;

        var accepted = request.Readings
            .Where(r => knownVehicleIds.Contains(r.VehicleId))
            .Select(r => new TelemetryReadingData(
                r.VehicleId,
                r.Latitude,
                r.Longitude,
                r.BatteryPercentage,
                r.RecordedAt is { } recordedAt ? ToUtc(recordedAt) : receivedAt))
            .ToList();

        if (accepted.Count > 0 && !_telemetryChannel.TryWriteBatch(accepted))
        {
            Response.Headers.RetryAfter = RetryAfterSeconds.ToString(CultureInfo.InvariantCulture);
            return Problem(
                detail: "Telemetri kuyruğu dolu. Lütfen kısa süre sonra tekrar gönderin.",
                statusCode: StatusCodes.Status503ServiceUnavailable,
                title: "Geçici olarak kullanılamıyor");
        }

        var rejected = vehicleIds.Where(id => !knownVehicleIds.Contains(id)).ToList();

        return Accepted(new TelemetryBatchResponse(accepted.Count, rejected));
    }

    private static DateTime ToUtc(DateTime value) => value.Kind switch
    {
        DateTimeKind.Utc => value,
        DateTimeKind.Local => value.ToUniversalTime(),
        _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
    };
}
