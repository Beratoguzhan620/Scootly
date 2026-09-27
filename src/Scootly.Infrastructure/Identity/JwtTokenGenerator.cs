using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scootly.Application.Abstractions;

namespace Scootly.Infrastructure.Identity;

public sealed class JwtTokenGenerator
{
    private readonly JwtOptions _options;
    private readonly IClock _clock;

    public JwtTokenGenerator(IOptions<JwtOptions> options, IClock clock)
    {
        _options = options.Value;
        _clock = clock;
    }

    public string GenerateUserToken(ApplicationUser user, IEnumerable<string> roles)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Email, user.Email ?? string.Empty),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ScootlyClaimTypes.ClientType, ScootlyClaimTypes.UserClient)
        };

        if (!string.IsNullOrEmpty(user.HomeRegion))
            claims.Add(new Claim("homeRegion", user.HomeRegion));

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        return WriteToken(claims, TimeSpan.FromMinutes(_options.ExpiryMinutes));
    }

    public string GenerateDeviceToken(string clientId, TimeSpan lifetime)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, clientId),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, clientId),
            new(ClaimTypes.Role, ScootlyRoles.Device),
            new(ScootlyClaimTypes.ClientType, ScootlyClaimTypes.DeviceClient)
        };

        return WriteToken(claims, lifetime);
    }

    private string WriteToken(IEnumerable<Claim> claims, TimeSpan lifetime)
    {
        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);
        var now = _clock.UtcNow;

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: now,
            expires: now.Add(lifetime),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
