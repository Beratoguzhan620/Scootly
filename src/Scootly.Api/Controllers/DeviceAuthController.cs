using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Scootly.Api.Contracts.Requests;
using Scootly.Api.Contracts.Responses;
using Scootly.Api.Extensions;
using Scootly.Infrastructure.Identity;

namespace Scootly.Api.Controllers;

[ApiController]
[Route("api/device-auth")]
[AllowAnonymous]
[EnableRateLimiting(RateLimitPolicies.Auth)]
public sealed class DeviceAuthController : ControllerBase
{
    private readonly DeviceTokenService _deviceTokenService;

    public DeviceAuthController(DeviceTokenService deviceTokenService)
    {
        _deviceTokenService = deviceTokenService;
    }

    [HttpPost("token")]
    [ProducesResponseType<TokenResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public IActionResult IssueToken([FromBody] DeviceTokenRequest request)
    {
        var token = _deviceTokenService.IssueToken(request.ClientId, request.ClientSecret);

        if (token is null)
            return Unauthorized();

        return Ok(new TokenResponse(token));
    }
}
