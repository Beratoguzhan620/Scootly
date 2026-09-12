using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Scootly.Application.Abstractions;

namespace Scootly.Infrastructure.Identity;

/// <summary>
/// Kullanıcı için imzalı bir erişim token'ı üretir.
/// </summary>
/// <remarks>
/// <para>
/// Token'ın gövdesi ŞİFRELİ DEĞİLDİR, yalnızca base64 ile kodlanmıştır. İmza,
/// içeriğin değiştirilmediğini kanıtlar; içeriğin gizli kaldığını değil.
/// Token'ı eline geçiren herkes içindeki her şeyi okuyabilir. Bu yüzden içine
/// yalnızca yetkilendirme kararı için gereken en az bilgi konur:
/// kimlik, rol ve (varsa) bölge. Parola, e-posta, telefon, konum ve benzeri
/// hiçbir kişisel veri konmaz.
/// </para>
/// <para>
/// Zaman <see cref="IClock"/> üzerinden okunur, <c>DateTime.UtcNow</c> ile doğrudan
/// değil — böylece süre dolması davranışı testte sahte bir saatle sınanabilir.
/// </para>
/// </remarks>
public sealed class JwtTokenGenerator
{
    private readonly JwtOptions _options;
    private readonly IClock _clock;
    private readonly SigningCredentials _signingCredentials;

    public JwtTokenGenerator(JwtOptions options, IClock clock)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(clock);

        // Yapılandırma burada bir kez daha doğrulanır. Program.cs zaten açılışta
        // çağırıyor; buradaki çağrı, bu sınıfı testte elle kuran birinin
        // geçersiz bir yapılandırmayla sessizce çalışmasını engelliyor.
        options.Validate();

        _options = options;
        _clock = clock;

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
        _signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
    }

    public string GenerateToken(ApplicationUser user, IEnumerable<string> roles)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(roles);

        var now = _clock.UtcNow;

        var claims = new List<Claim>
        {
            new(ScootlyClaimTypes.Subject, user.Id.ToString()),
            // Her token'a tekil bir kimlik. Bugün kullanılmıyor; 4. fazda
            // tekrar oynatma (replay) tespiti ve iptal listesi için gerekecek.
            new(ScootlyClaimTypes.TokenId, Guid.NewGuid().ToString())
        };

        foreach (var role in roles)
        {
            if (!string.IsNullOrWhiteSpace(role))
            {
                claims.Add(new Claim(ScootlyClaimTypes.Role, role));
            }
        }

        // Bölge yalnızca dolu olduğunda eklenir. Boş bir iddia eklemek, her
        // sürücü token'ını gereksiz büyütür ve "bölgesi yok" ile "bölgesi boş"
        // ayrımını doğrulama tarafında bulanıklaştırır.
        if (!string.IsNullOrWhiteSpace(user.HomeRegion))
        {
            claims.Add(new Claim(ScootlyClaimTypes.HomeRegion, user.HomeRegion));
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now,
            NotBefore = now,
            Expires = now.AddMinutes(_options.AccessTokenLifetimeMinutes),
            SigningCredentials = _signingCredentials
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    /// <summary>Token'ın ne zaman geçersiz olacağı — istemciye bildirmek için.</summary>
    public DateTime ExpiresAt() => _clock.UtcNow.AddMinutes(_options.AccessTokenLifetimeMinutes);
}
