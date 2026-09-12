namespace Scootly.Infrastructure.Identity;

/// <summary>
/// Token içindeki iddia (claim) adları.
/// </summary>
/// <remarks>
/// Kısa adlar (<c>sub</c>, <c>role</c>) bilinçli tercih. .NET'in varsayılan
/// davranışı, iddia adlarını uzun URI'lere çevirmektir; bu URI'ler her token'ı
/// gereksiz büyütür. Bedeli, doğrulama tarafında <c>RoleClaimType</c> ve
/// <c>NameClaimType</c>'ın da aynı kısa adlara ayarlanması gerekmesidir —
/// unutulursa <c>[Authorize(Roles = ...)]</c> hiçbir rolü bulamaz ve her istek
/// sessizce 403 döner.
/// </remarks>
public static class ScootlyClaimTypes
{
    /// <summary>Kullanıcının ya da cihazın kimliği (JWT standardı: subject).</summary>
    public const string Subject = "sub";

    /// <summary>Token'ın tekil kimliği (JWT standardı: JWT ID).</summary>
    public const string TokenId = "jti";

    /// <summary>Rol. Birden fazla rol varsa iddia tekrarlanır.</summary>
    public const string Role = "role";

    /// <summary>Operatörün/yöneticinin sorumlu olduğu bölge. Sürücülerde bulunmaz.</summary>
    public const string HomeRegion = "home_region";

    /// <summary>
    /// Araç cihazının kimliği (25. gün). Yalnızca cihaz token'larında bulunur.
    /// <c>sub</c> ile aynı değeri taşır; ayrı bir iddia olmasının nedeni,
    /// "bu bir cihaz isteği mi" sorusunun rol iddiasına bakmadan, tek ve
    /// belirgin bir alandan cevaplanabilmesi.
    /// </summary>
    public const string DeviceId = "device_id";
}
