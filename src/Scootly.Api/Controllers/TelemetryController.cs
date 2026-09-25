using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scootly.Api.Contracts.Requests;
using Scootly.Application.Abstractions;
using Scootly.Domain.Geo;
using Scootly.Domain.Telemetry;

namespace Scootly.Api.Controllers;

[ApiController]
[Route("api/telemetry")]
public sealed class TelemetryController : ControllerBase
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IClock _clock;

    public TelemetryController(IApplicationDbContext dbContext, IClock clock)
    {
        _dbContext = dbContext;
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

            _dbContext.AddTelemetryReading(reading);
        }

        await _dbContext.SaveChangesAsync();

        return Ok(new { Received = request.Readings.Count });
    }
}