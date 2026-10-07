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
            new(ScootlyClaimTypes.ClientType, ScootlyClaimTypes.UserClient),
            new(ScootlyClaimTypes.SecurityStamp, user.SecurityStamp ?? string.Empty)
        };

        claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

        return JwtTokenWriter.Write(
            claims, _options.Issuer, _options.Audience, _options.Key, _clock.UtcNow, TimeSpan.FromMinutes(_options.ExpiryMinutes));
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

        return JwtTokenWriter.Write(claims, _options.Issuer, _options.Audience, _options.Key, _clock.UtcNow, lifetime);
    }
}

/// <summary>Yalnızca SignalR hub'ında geçerli token üretir (bkz. <see cref="HubTokenOptions"/>). Rol taşımaz.</summary>
public sealed class HubTokenGenerator
{
    private readonly HubTokenOptions _options;
    private readonly IClock _clock;

    public HubTokenGenerator(IOptions<HubTokenOptions> options, IClock clock)
    {
        _options = options.Value;
        _clock = clock;
    }

    public string Generate(ApplicationUser user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new(ScootlyClaimTypes.ClientType, ScootlyClaimTypes.UserClient),
            new(ScootlyClaimTypes.SecurityStamp, user.SecurityStamp ?? string.Empty)
        };

        return JwtTokenWriter.Write(
            claims, _options.Issuer, _options.HubAudience, _options.HubKey, _clock.UtcNow, TimeSpan.FromMinutes(_options.HubTokenMinutes));
    }
}

internal static class JwtTokenWriter
{
    public static string Write(IEnumerable<Claim> claims, string issuer, string audience, string key, DateTime now, TimeSpan lifetime)
    {
        var credentials = new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)), SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            notBefore: now,
            expires: now.Add(lifetime),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}
