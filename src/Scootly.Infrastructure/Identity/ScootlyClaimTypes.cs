namespace Scootly.Infrastructure.Identity;

/// <summary>
/// Token içindeki iddia (claim) adları.
/// </summary>
/// <remarks>
/// Kısa adlar (<c>sub</c>, <c>role</c>) bilinçli tercih. .NET'in varsayılan davranışı,
/// iddia adlarını uzun URI'lere çevirmektir
/// (<c>http://schemas.microsoft.com/ws/2008/06/identity/claims/role</c>). Bu URI'ler
/// her token'ı gereksiz yere büyütür ve her istekte tel üzerinden taşınır.
/// Kısa ad kullanmanın bedeli, doğrulama tarafında <c>RoleClaimType</c> ve
/// <c>NameClaimType</c>'ın da aynı kısa adlara ayarlanması gerekmesidir —
/// unutulursa <c>[Authorize(Roles = ...)]</c> hiçbir rolü bulamaz ve her istek
/// sessizce 403 döner. Program.cs'te bu ayar açıkça yapılıyor.
/// </remarks>
public static class ScootlyClaimTypes
{
    /// <summary>Kullanıcının kimliği (JWT standardı: subject).</summary>
    public const string Subject = "sub";

    /// <summary>Token'ın tekil kimliği (JWT standardı: JWT ID).</summary>
    public const string TokenId = "jti";

    /// <summary>Kullanıcının rolü. Birden fazla rol varsa iddia tekrarlanır.</summary>
    public const string Role = "role";

    /// <summary>Operatörün/yöneticinin sorumlu olduğu bölge. Sürücülerde bulunmaz.</summary>
    public const string HomeRegion = "home_region";
}
