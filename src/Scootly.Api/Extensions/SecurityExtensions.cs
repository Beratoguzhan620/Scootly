using System.Security.Claims;
using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scootly.Infrastructure.Identity;

namespace Scootly.Api.Extensions;

public static class SecurityExtensions
{
    public const string HubsPathPrefix = "/hubs";

    /// <summary>Mvc'nin ürettiği, yalnızca hub'da geçerli token'ları doğrulayan şema (ayrı anahtar, ayrı hedef kitle).</summary>
    public const string HubScheme = "HubBearer";

    /// <summary>Hub'ın kabul ettiği şemalar: istemcilerin normal API token'ı ve Mvc'nin hub token'ı.</summary>
    public const string HubSchemes = $"{JwtBearerDefaults.AuthenticationScheme},{HubScheme}";

    public static IServiceCollection AddScootlyAuthentication(this IServiceCollection services)
    {
        // Varsayılan şema yalnızca ana anahtarla imzalı API token'larını kabul eder; hub token'ı API uçlarında geçersizdir.
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer()
            .AddJwtBearer(HubScheme);

        // JWT ayarları çalışma anında, doğrulanmış seçeneklerden okunur (anahtarlar kaynak kodda değil).
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, options) =>
                Configure(bearer, options.Value.Issuer, options.Value.Audience, options.Value.Key));

        services.AddOptions<JwtBearerOptions>(HubScheme)
            .Configure<IOptions<HubTokenOptions>>((bearer, options) =>
                Configure(bearer, options.Value.Issuer, options.Value.HubAudience, options.Value.HubKey));

        return services;
    }

    private static void Configure(JwtBearerOptions bearer, string issuer, string audience, string key)
    {
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = issuer,
            ValidAudience = audience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key)),
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        bearer.Events = new JwtBearerEvents
        {
            // Tarayıcıdaki SignalR istemcileri WebSocket bağlantısında başlık gönderemez; token sorgu dizesiyle gelir.
            OnMessageReceived = context =>
            {
                var accessToken = context.Request.Query["access_token"];

                if (!string.IsNullOrEmpty(accessToken) && context.HttpContext.Request.Path.StartsWithSegments(HubsPathPrefix))
                    context.Token = accessToken;

                return Task.CompletedTask;
            },
            OnTokenValidated = ValidateUserSessionAsync
        };
    }

    /// <summary>
    /// Kullanıcı token'ındaki güvenlik damgası güncel değilse (rol/parola değişti, hesap silindi) token reddedilir.
    /// Cihaz token'ları bir Identity kullanıcısına ait olmadığı için bu kontrole girmez.
    /// </summary>
    private static async Task ValidateUserSessionAsync(TokenValidatedContext context)
    {
        var principal = context.Principal;

        if (principal?.FindFirstValue(ScootlyClaimTypes.ClientType) != ScootlyClaimTypes.UserClient)
            return;

        if (!Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
        {
            context.Fail("Token geçerli bir kullanıcı kimliği taşımıyor.");
            return;
        }

        var validator = context.HttpContext.RequestServices.GetRequiredService<UserSessionValidator>();
        var securityStamp = principal.FindFirstValue(ScootlyClaimTypes.SecurityStamp);

        if (!await validator.IsValidAsync(userId, securityStamp, context.HttpContext.RequestAborted))
            context.Fail("Oturum geçersiz kılındı; lütfen yeniden giriş yapın.");
    }
}
