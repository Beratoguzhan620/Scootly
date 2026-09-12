using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Scootly.Application.Abstractions;
using Scootly.Infrastructure.Identity;
using Scootly.Infrastructure.Persistence;

namespace Scootly.Infrastructure.Devices;

/// <summary>
/// Araç cihazları için token üretir (OAuth 2.0 "client credentials" akışının
/// sadeleştirilmiş hali: cihaz kendi kimliği ve sırrıyla doğrudan token alır).
/// </summary>
public sealed class DeviceTokenService
{
    /// <summary>
    /// Cihaz token'ının hedef kitlesi, kullanıcı token'ından FARKLI.
    /// Bu, tek satırla anlatılabilecek en önemli kısıt: bir cihaz token'ı
    /// kullanıcı uçlarında doğrulanamaz, çünkü beklenen audience tutmaz.
    /// Aynı audience kullanılsaydı, cihaz sırrı sızan bir saldırgan cihaz
    /// token'ıyla kullanıcı uçlarına da gidebilirdi.
    /// </summary>
    public const string DeviceAudience = "scootly-devices";

    /// <summary>
    /// Cihaz token'ı kullanıcı token'ından KISA yaşar. Cihazlar sahada,
    /// fiziksel erişime açık yerlerde duruyor; sızan bir token'ın geçerlilik
    /// penceresi mümkün olduğunca dar olmalı.
    /// </summary>
    public const int DeviceTokenLifetimeMinutes = 15;

    private readonly ScootlyDbContext _dbContext;
    private readonly IPasswordHasher<DeviceCredential> _hasher;
    private readonly JwtOptions _jwtOptions;
    private readonly IClock _clock;

    public DeviceTokenService(
        ScootlyDbContext dbContext,
        IPasswordHasher<DeviceCredential> hasher,
        JwtOptions jwtOptions,
        IClock clock)
    {
        _dbContext = dbContext;
        _hasher = hasher;
        _jwtOptions = jwtOptions;
        _clock = clock;
    }

    /// <summary>
    /// Cihaz kimliği ve sırrı doğruysa token döner, değilse <c>null</c>.
    /// </summary>
    /// <remarks>
    /// Cihaz bulunamadığında da bir hash doğrulaması çalıştırılır — giriş
    /// ucundaki ile aynı gerekçe: yanıt süresi, hangi cihaz kimliklerinin
    /// kayıtlı olduğunu sızdırmasın.
    /// </remarks>
    public async Task<string?> IssueTokenAsync(
        string deviceId,
        string secret,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(deviceId) || string.IsNullOrWhiteSpace(secret))
        {
            return null;
        }

        var cihaz = await _dbContext.DeviceCredentials
            .AsNoTracking()
            .SingleOrDefaultAsync(d => d.DeviceId == deviceId, cancellationToken);

        if (cihaz is null || !cihaz.IsActive)
        {
            _hasher.VerifyHashedPassword(ZamanDengeleyici, ZamanDengeleyiciHash, secret);
            return null;
        }

        var dogrulama = _hasher.VerifyHashedPassword(cihaz, cihaz.SecretHash, secret);
        if (dogrulama == PasswordVerificationResult.Failed)
        {
            return null;
        }

        return Uret(cihaz.DeviceId);
    }

    /// <summary>Bir cihaz sırrını saklanabilir hale getirir (kayıt/rotasyon için).</summary>
    public string HashSecret(string deviceId, string secret) =>
        _hasher.HashPassword(new DeviceCredential(deviceId, string.Empty), secret);

    private static readonly DeviceCredential ZamanDengeleyici = new("bulunmayan-cihaz", string.Empty);

    private static readonly string ZamanDengeleyiciHash =
        new PasswordHasher<DeviceCredential>()
            .HashPassword(ZamanDengeleyici, "bu-deger-hicbir-cihaza-ait-degil");

    private string Uret(string deviceId)
    {
        var now = _clock.UtcNow;

        var iddialar = new List<Claim>
        {
            new(ScootlyClaimTypes.Subject, deviceId),
            new(ScootlyClaimTypes.TokenId, Guid.NewGuid().ToString()),
            new(ScootlyClaimTypes.Role, RoleNames.VehicleDevice),
            new(ScootlyClaimTypes.DeviceId, deviceId)
        };

        var anahtar = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwtOptions.SigningKey));

        var tanim = new SecurityTokenDescriptor
        {
            Issuer = _jwtOptions.Issuer,
            Audience = DeviceAudience,
            Subject = new ClaimsIdentity(iddialar),
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(DeviceTokenLifetimeMinutes),
            SigningCredentials = new SigningCredentials(anahtar, SecurityAlgorithms.HmacSha256)
        };

        return new JsonWebTokenHandler().CreateToken(tanim);
    }
}
