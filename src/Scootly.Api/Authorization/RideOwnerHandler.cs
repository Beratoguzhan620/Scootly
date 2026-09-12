using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Scootly.Domain.Riding;
using Scootly.Infrastructure.Identity;

namespace Scootly.Api.Authorization;

/// <summary>
/// Sürüşün, isteği yapan kullanıcıya ait olup olmadığına karar verir.
/// </summary>
/// <remarks>
/// Rol tabanlı yetkinin cevaplayamadığı soru buydu: "bu bir sürücü mü" değil,
/// "bu sürücü BU sürüşün sahibi mi". Cevap, kaynağın kendisine bakmadan
/// verilemez — bu yüzden handler kaynağı parametre olarak alıyor.
/// </remarks>
public sealed class RideOwnerHandler : AuthorizationHandler<RideOwnerRequirement, Ride>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        RideOwnerRequirement requirement,
        Ride resource)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(resource);

        var ham = context.User.FindFirstValue(ScootlyClaimTypes.Subject);

        // Guid.Empty ayrıca eleniyor. Kimliği doğrulanmamış bir istekte
        // CurrentUserAccessor Guid.Empty döndürüyor; DriverId alanı da bir
        // hata sonucu Guid.Empty olsaydı, ikisi eşleşir ve kimliksiz bir istek
        // sahiplik kontrolünden geçerdi.
        if (Guid.TryParse(ham, out var kullaniciId)
            && kullaniciId != Guid.Empty
            && kullaniciId == resource.DriverId)
        {
            context.Succeed(requirement);
        }

        // Başarısızlıkta context.Fail() ÇAĞRILMIYOR.
        // Fail(), aynı gereksinimi karşılayabilecek başka bir handler başarılı
        // olsa bile kararı kesin olarak olumsuz yapar. Burada istenen bu değil:
        // ileride "denetçi her sürüşe bakabilir" diye ikinci bir handler
        // eklenirse, bu handler sessizce çekilip ona yol vermeli.
        // Hiçbir handler Succeed demezse sonuç zaten olumsuzdur.
        return Task.CompletedTask;
    }
}
