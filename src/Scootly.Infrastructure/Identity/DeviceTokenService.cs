using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Scootly.Infrastructure.Identity;

public sealed class DeviceTokenService
{
    private readonly DeviceAuthOptions _options;
    private readonly JwtTokenGenerator _tokenGenerator;

    public DeviceTokenService(IOptions<DeviceAuthOptions> options, JwtTokenGenerator tokenGenerator)
    {
        _options = options.Value;
        _tokenGenerator = tokenGenerator;
    }

    /// <returns>Kimlik bilgileri doğruysa cihaz token'ı, aksi halde null.</returns>
    public string? IssueToken(string? clientId, string? clientSecret)
    {
        // Yapılandırma eksikse hiçbir istemci doğrulanamaz (fail closed).
        if (string.IsNullOrEmpty(_options.ClientId) || string.IsNullOrEmpty(_options.ClientSecret))
            return null;

        // Her iki karşılaştırma da her zaman yapılır ve sabit sürelidir; hangi alanın yanlış olduğu zamanlamadan anlaşılamaz.
        var clientIdMatches = SecretEquals(clientId, _options.ClientId);
        var secretMatches = SecretEquals(clientSecret, _options.ClientSecret);

        if (!(clientIdMatches & secretMatches))
            return null;

        return _tokenGenerator.GenerateDeviceToken(_options.ClientId, TimeSpan.FromMinutes(_options.TokenLifetimeMinutes));
    }

    /// <summary>Uzunluk bilgisini de sızdırmamak için değerlerin SHA-256 özetlerini sabit sürede karşılaştırır.</summary>
    private static bool SecretEquals(string? provided, string expected)
    {
        var providedHash = SHA256.HashData(Encoding.UTF8.GetBytes(provided ?? string.Empty));
        var expectedHash = SHA256.HashData(Encoding.UTF8.GetBytes(expected));

        return CryptographicOperations.FixedTimeEquals(providedHash, expectedHash);
    }
}
