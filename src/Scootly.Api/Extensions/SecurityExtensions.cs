using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scootly.Api.Authorization;
using Scootly.Infrastructure.Identity;

namespace Scootly.Api.Extensions;

public static class SecurityExtensions
{
    public const string HubsPathPrefix = "/hubs";

    public static IServiceCollection AddScootlyAuthentication(this IServiceCollection services)
    {
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer();

        // JWT ayarları çalışma anında, doğrulanmış JwtOptions'tan okunur (anahtar kaynak kodda değil).
        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<JwtOptions>>((bearer, jwtOptions) =>
            {
                var jwt = jwtOptions.Value;

                bearer.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    ValidateIssuerSigningKey = true,
                    ValidIssuer = jwt.Issuer,
                    ValidAudience = jwt.Audience,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key)),
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
                    }
                };
            });

        return services;
    }

    public static IServiceCollection AddScootlyAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()
            // Varsayılan olarak güvenli: [AllowAnonymous] olmayan her uç kimlik doğrulaması ister.
            .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
            .AddPolicy(PolicyNames.DriverOnly, policy => policy
                .RequireClaim(ScootlyClaimTypes.ClientType, ScootlyClaimTypes.UserClient)
                .RequireRole(ScootlyRoles.Driver))
            .AddPolicy(PolicyNames.FleetManagerOnly, policy => policy
                .RequireClaim(ScootlyClaimTypes.ClientType, ScootlyClaimTypes.UserClient)
                .RequireRole(ScootlyRoles.FleetManager))
            .AddPolicy(PolicyNames.FleetOperations, policy => policy
                .RequireClaim(ScootlyClaimTypes.ClientType, ScootlyClaimTypes.UserClient)
                .RequireRole(ScootlyRoles.FleetManager, ScootlyRoles.FieldOperator))
            .AddPolicy(PolicyNames.DeviceOnly, policy => policy
                .RequireClaim(ScootlyClaimTypes.ClientType, ScootlyClaimTypes.DeviceClient)
                .RequireRole(ScootlyRoles.Device))
            .AddPolicy(PolicyNames.RideOwner, policy => policy
                .AddRequirements(new RideOwnerRequirement()));

        services.AddSingleton<IAuthorizationHandler, RideOwnerHandler>();

        return services;
    }
}
