using Microsoft.AspNetCore.Identity;

namespace Scootly.Infrastructure.Identity;

/// <summary>
/// Scootly kullanıcısı. ASP.NET Core Identity'nin taban kullanıcısını,
/// birincil anahtar tipi <see cref="Guid"/> olacak şekilde genişletir.
/// </summary>
/// <remarks>
/// Identity'nin varsayılanı <c>string</c> anahtardır. Guid seçilmesinin nedeni,
/// alan modelinde kullanıcıya yapılan her atıfın (<c>Ride.DriverId</c>,
/// <c>ICurrentUser.UserId</c>) zaten Guid olmasıdır; string anahtar seçilseydi
/// her sınırda Parse/ToString dönüşümü yapılması gerekir, her dönüşüm de
/// sessizce başarısız olabilecek bir nokta açardı.
/// </remarks>
public sealed class ApplicationUser : IdentityUser<Guid>
{
    /// <summary>
    /// Kullanıcının sorumlu olduğu bölge (saha operatörü ve filo yöneticisi için).
    /// Sürücülerde anlamı yoktur, bu yüzden nullable.
    /// </summary>
    public string? HomeRegion { get; set; }
}
