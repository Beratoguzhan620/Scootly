using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using Scootly.Application.Abstractions;
using Scootly.Infrastructure.Identity;
using Scootly.Infrastructure.Payments;
using Xunit;

namespace Scootly.Infrastructure.Tests;

internal sealed class FixedClock : IClock
{
    public DateTime UtcNow { get; set; } = new(2026, 9, 27, 12, 0, 0, DateTimeKind.Utc);
}

public sealed class PaymentWebhookValidatorTests
{
    private const string Secret = "0123456789abcdef0123456789abcdef-webhook";
    private const string Body = """{"eventId":"1","rideId":"2","success":true}""";

    private readonly FixedClock _clock = new();
    private readonly PaymentWebhookValidator _validator;

    public PaymentWebhookValidatorTests()
    {
        _validator = new PaymentWebhookValidator(Options.Create(new PaymentWebhookOptions { Secret = Secret, ToleranceSeconds = 300 }), _clock);
    }

    private long Now => new DateTimeOffset(_clock.UtcNow).ToUnixTimeSeconds();

    [Fact]
    public void Dogru_Imza_Kabul_Edilmeli()
    {
        Assert.True(_validator.IsValid(Body, PaymentWebhookValidator.CreateHeaderValue(Secret, Now, Body)));
    }

    [Fact]
    public void Govde_Degisirse_Imza_Gecersiz_Olmali()
    {
        var header = PaymentWebhookValidator.CreateHeaderValue(Secret, Now, Body);

        Assert.False(_validator.IsValid(Body.Replace("true", "false"), header));
    }

    [Fact]
    public void Farkli_Anahtarla_Imza_Gecersiz_Olmali()
    {
        Assert.False(_validator.IsValid(Body, PaymentWebhookValidator.CreateHeaderValue("baska-anahtar-baska-anahtar-baska-anahtar", Now, Body)));
    }

    [Theory]
    [InlineData(-301)]
    [InlineData(301)]
    public void Tolerans_Disindaki_Zaman_Damgasi_Reddedilmeli(int offsetSeconds)
    {
        Assert.False(_validator.IsValid(Body, PaymentWebhookValidator.CreateHeaderValue(Secret, Now + offsetSeconds, Body)));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("imza")]
    [InlineData("t=abc,v1=00")]
    [InlineData("t=1,v1=zz")]
    [InlineData("v1=aa")]
    public void Bozuk_Baslik_Reddedilmeli(string? header)
    {
        Assert.False(_validator.IsValid(Body, header));
    }
}

public sealed class DeviceTokenServiceTests
{
    private const string ClientId = "scootly-device-simulator";
    private const string ClientSecret = "cihaz-sirri-cihaz-sirri-cihaz-sirri-1234";

    private static DeviceTokenService CreateService(string clientId = ClientId, string clientSecret = ClientSecret)
    {
        var jwt = new JwtTokenGenerator(Options.Create(new JwtOptions
        {
            Key = "test-imza-anahtari-test-imza-anahtari-1234567890",
            Issuer = "Scootly.Api",
            Audience = "Scootly.Clients",
            ExpiryMinutes = 60
        }), new FixedClock { UtcNow = DateTime.UtcNow });

        return new DeviceTokenService(Options.Create(new DeviceAuthOptions
        {
            ClientId = clientId,
            ClientSecret = clientSecret,
            TokenLifetimeMinutes = 60
        }), jwt);
    }

    [Fact]
    public void Dogru_Kimlik_Bilgileri_Cihaz_Rolu_Ve_Istemci_Turu_Tasiyan_Token_Uretmeli()
    {
        var token = CreateService().IssueToken(ClientId, ClientSecret);

        Assert.NotNull(token);
        var claims = new JwtSecurityTokenHandler().ReadJwtToken(token).Claims.ToList();
        Assert.Contains(claims, c => c.Type is "role" or ClaimTypes.Role && c.Value == ScootlyRoles.Device);
        Assert.Contains(claims, c => c.Type == ScootlyClaimTypes.ClientType && c.Value == ScootlyClaimTypes.DeviceClient);
    }

    [Theory]
    [InlineData(ClientId, "yanlis")]
    [InlineData("yanlis", ClientSecret)]
    [InlineData(null, null)]
    [InlineData("", "")]
    public void Yanlis_Kimlik_Bilgileri_Reddedilmeli(string? clientId, string? clientSecret)
    {
        Assert.Null(CreateService().IssueToken(clientId, clientSecret));
    }

    [Fact]
    public void Yapilandirma_Eksikse_Hicbir_Istemci_Dogrulanmamali()
    {
        Assert.Null(CreateService(clientSecret: string.Empty).IssueToken(ClientId, string.Empty));
    }
}
