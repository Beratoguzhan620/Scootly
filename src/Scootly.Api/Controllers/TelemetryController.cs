using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scootly.Api.Contracts.Requests;
using Scootly.Application.Abstractions;
using Scootly.Application.Telemetry;
using Scootly.Domain.Geo;
using Scootly.Domain.Telemetry;

namespace Scootly.Api.Controllers;

[ApiController]
[Route("api/telemetry")]
public sealed class TelemetryController : ControllerBase
{
    private readonly TelemetryChannel _telemetryChannel;
    private readonly IClock _clock;

    public TelemetryController(TelemetryChannel telemetryChannel, IClock clock)
    {
        _telemetryChannel = telemetryChannel;
        _clock = clock;
    }

    [HttpPost("batch")]
    [Authorize]
    public async Task<IActionResult> IngestBatch([FromBody] TelemetryBatchRequest request)
    {
        foreach (var item in request.Readings)
        {
            var reading = new TelemetryReading(
                Guid.NewGuid(),
                item.VehicleId,
                new GeoPoint(item.Latitude, item.Longitude),
                item.BatteryPercentage,
                _clock.UtcNow);

            await _telemetryChannel.WriteAsync(reading);
        }

        return Ok(new { Received = request.Readings.Count });
    }
}