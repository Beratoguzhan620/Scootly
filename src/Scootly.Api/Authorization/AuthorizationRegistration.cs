using Microsoft.AspNetCore.Authorization;
using Scootly.Infrastructure.Identity;

namespace Scootly.Api.Authorization;

public static class AuthorizationRegistration
{
    /// <summary>
    /// Politikaları kaydeder. Program.cs içine gömmek yerine ayrı bir metot
    /// olmasının nedeni test edilebilirlik: politikaların gerçekten doğru kararı
    /// verdiği, web sunucusu ayağa kaldırılmadan sınanabiliyor.
    /// </summary>
    public static IServiceCollection AddScootlyAuthorization(this IServiceCollection services)
    {
        services.AddAuthorizationBuilder()

            // VARSAYILAN: kimlik doğrulaması olmayan hiçbir istek geçmez.
            //
            // Bunun alternatifi, her ucu tek tek [Authorize] ile işaretlemektir —
            // yani korumayı OPT-IN yapmaktır. O modelde yeni eklenen bir uç,
            // birileri işareti koymayı unuttuğu anda sessizce herkese açık olur
            // ve bu unutkanlık hiçbir hata üretmez. Burada tersi geçerli:
            // yeni uç varsayılan olarak KAPALIDIR, açmak isteyen [AllowAnonymous]
            // yazmak zorundadır — ve bu, gözden geçirmede görünen bir satırdır.
            .SetFallbackPolicy(new AuthorizationPolicyBuilder()
                .RequireAuthenticatedUser()
                .Build())

            // --- Rol tabanlı politikalar ---
            .AddPolicy(PolicyNames.SadeceYonetici, policy =>
                policy.RequireRole(RoleNames.FleetManager))

            .AddPolicy(PolicyNames.SadeceOperator, policy =>
                policy.RequireRole(RoleNames.FieldOperator))

            .AddPolicy(PolicyNames.SadeceSurucu, policy =>
                policy.RequireRole(RoleNames.Driver))

            .AddPolicy(PolicyNames.SadeceDenetci, policy =>
                policy.RequireRole(RoleNames.Auditor))

            // --- İddia tabanlı politika ---
            // Rolden bağımsız: kullanıcının bir bölgeye bağlı olmasını şart koşar.
            .AddPolicy(PolicyNames.BolgeliPersonel, policy =>
                policy.RequireClaim(ScootlyClaimTypes.HomeRegion));

        return services;
    }
}
