using Microsoft.AspNetCore.Authorization;
using Scootly.Infrastructure.Identity;

namespace Scootly.Api.Authorization;

public static class AuthorizationRegistration
{
    /// <summary>
    /// Politikaları ve kaynak tabanlı kural işleyicilerini kaydeder.
    /// Program.cs içine gömmek yerine ayrı bir metot olmasının nedeni test
    /// edilebilirlik: kararların doğruluğu, web sunucusu ayağa kaldırılmadan
    /// sınanabiliyor.
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
            .AddPolicy(PolicyNames.BolgeliPersonel, policy =>
                policy.RequireClaim(ScootlyClaimTypes.HomeRegion))

            // --- Kaynak tabanlı politikalar (24. gün) ---
            // Bu politikalar bir kaynak olmadan karar veremez. Öznitelikle
            // uygulanamazlar; controller içinde AuthorizeAsync(User, kaynak, ...)
            // ile çağrılırlar.
            .AddPolicy(PolicyNames.SurusSahibi, policy =>
                policy.RequireAuthenticatedUser()
                      .AddRequirements(new RideOwnerRequirement()))

            .AddPolicy(PolicyNames.OperatorBolgesi, policy =>
                policy.RequireRole(RoleNames.FieldOperator)
                      .AddRequirements(new OperatorRegionRequirement()));

        // Kuralların mantığı burada devreye giriyor. Kayıt unutulursa politika
        // var olur ama hiçbir handler onu karşılamaz; sonuç her istekte 403
        // olur — sessiz değil, gürültülü bir başarısızlık. Ters yönde bir hata
        // (politikanın herkesi geçirmesi) mümkün değil, çünkü karşılanmamış bir
        // gereksinim daima olumsuz sonuç verir.
        services.AddScoped<IAuthorizationHandler, RideOwnerHandler>();
        services.AddScoped<IAuthorizationHandler, OperatorRegionHandler>();

        return services;
    }
}
