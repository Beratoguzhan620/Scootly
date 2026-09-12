using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Scootly.Api.Contracts.Requests;
using Scootly.Api.Contracts.Responses;
using Scootly.Application.Abstractions;
using Scootly.Infrastructure.Devices;

namespace Scootly.Api.Controllers;

[ApiController]
[Route("api/v1/device-auth")]
[AllowAnonymous]
public sealed class DeviceAuthController : ControllerBase
{
    private const string BasarisizDetay = "Cihaz kimliği veya sırrı hatalı.";

    private readonly DeviceTokenService _deviceTokens;
    private readonly IClock _clock;

    public DeviceAuthController(DeviceTokenService deviceTokens, IClock clock)
    {
        _deviceTokens = deviceTokens;
        _clock = clock;
    }

    /// <summary>Cihaz kimliği ve sırrı karşılığında kısa ömürlü token verir.</summary>
    [HttpPost("token")]
    [ProducesResponseType(typeof(TokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiErrorResponse), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Token(
        [FromBody] DeviceTokenRequest request,
        CancellationToken cancellationToken)
    {
        var token = await _deviceTokens.IssueTokenAsync(request.DeviceId, request.Secret, cancellationToken);

        if (token is null)
        {
            // "Cihaz yok" ile "sır yanlış" ayrımı yapılmıyor — giriş ucundaki
            // gerekçenin aynısı. Cihaz kimlikleri genelde tahmin edilebilir
            // bir düzende (plaka, seri numarası) olduğu için burada sayım
            // riski kullanıcı e-postalarından daha yüksek.
            return Unauthorized(new ApiErrorResponse(
                "Cihaz doğrulanamadı",
                BasarisizDetay,
                StatusCodes.Status401Unauthorized));
        }

        return Ok(new TokenResponse(
            token,
            _clock.UtcNow.AddMinutes(DeviceTokenService.DeviceTokenLifetimeMinutes)));
    }
}
