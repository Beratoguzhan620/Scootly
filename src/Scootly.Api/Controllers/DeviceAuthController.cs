using Microsoft.AspNetCore.Mvc;
using Scootly.Api.Contracts.Requests;
using Scootly.Infrastructure.Identity;

namespace Scootly.Api.Controllers;

[ApiController]
[Route("api/device-auth")]
public sealed class DeviceAuthController : ControllerBase
{
    private readonly DeviceTokenService _deviceTokenService;

    public DeviceAuthController(DeviceTokenService deviceTokenService)
    {
        _deviceTokenService = deviceTokenService;
    }

    [HttpPost("token")]
    public IActionResult IssueToken([FromBody] DeviceTokenRequest request)
    {
        var token = _deviceTokenService.IssueToken(request.ClientId, request.ClientSecret);

        if (token is null)
            return Unauthorized();

        return Ok(new { Token = token });
    }
}