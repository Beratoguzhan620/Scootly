using Microsoft.AspNetCore.Mvc;
using Scootly.Api.Contracts.Requests;
using Scootly.Application.Riding.Commands;

[HttpPost("{id}/complete")]
public async Task<IActionResult> Complete(
    Guid id,
    [FromBody] CompleteRideRequest request,
    CancellationToken cancellationToken)
{
    if (request.DriverId == Guid.Empty)
        return BadRequest("Geçersiz istek.");

    if (request.EndLatitude is < -90 or > 90 || request.EndLongitude is < -180 or > 180)
        return BadRequest("Koordinat aralık dışında.");

    var command = new CompleteRideCommand(id, request.DriverId, request.EndLatitude, request.EndLongitude);
    var result = await _completeHandler.Handle(command, cancellationToken);

    if (!result.IsSuccess)
        return NotFound(result.Error);

    return NoContent();
}