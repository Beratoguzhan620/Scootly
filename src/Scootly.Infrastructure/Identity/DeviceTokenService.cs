using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace Scootly.Infrastructure.Identity;

public sealed class DeviceTokenService
{
    private readonly IConfiguration _configuration;

    public DeviceTokenService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public string? IssueToken(string clientId, string clientSecret)
    {
        var expectedClientId = _configuration["DeviceAuth:ClientId"];
        var expectedClientSecret = _configuration["DeviceAuth:ClientSecret"];

        if (clientId != expectedClientId || clientSecret != expectedClientSecret)
            return null;

        var key = _configuration["Jwt:Key"]!;
        var issuer = _configuration["Jwt:Issuer"];
        var audience = _configuration["Jwt:Audience"];

        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, clientId),
            new(ClaimTypes.Role, "Device")
        };

        var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
        var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: issuer,
            audience: audience,
            claims: claims,
            expires: DateTime.UtcNow.AddHours(24),
            signingCredentials: credentials);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}