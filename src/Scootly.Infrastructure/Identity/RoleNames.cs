namespace Scootly.Infrastructure.Identity;

/// <summary>
/// Sistemdeki rol adları. Serbest metin yerine sabit kullanılmasının nedeni,
/// yazım hatasının derleme zamanında değil çalışma zamanında ve sessizce
/// "yetkisiz" sonucu üretmesidir — yanlış yazılmış bir rol adı hiçbir
/// kullanıcıyla eşleşmez ama hata da vermez.
/// </summary>
public static class RoleNames
{
    /// <summary>Araç kiralayan son kullanıcı.</summary>
    public const string Driver = "Driver";

    /// <summary>Filoyu yöneten, araç ekleyip çıkaran kullanıcı.</summary>
    public const string FleetManager = "FleetManager";

    /// <summary>Sahada araç toplayan/şarj eden operatör.</summary>
    public const string FieldOperator = "FieldOperator";

    /// <summary>Yalnızca okuma yetkisi olan denetçi.</summary>
    public const string Auditor = "Auditor";

    /// <summary>
    /// Tohumlama (seed) ve testlerin üzerinden geçtiği tam liste.
    /// Ziyaretçi ve araç cihazı bu listede yok: ziyaretçi kimliksiz kullanıcıdır,
    /// araç cihazı ise 25. günde ayrı bir token akışıyla gelecek.
    /// </summary>
    public static readonly IReadOnlyList<string> All =
    [
        Driver,
        FleetManager,
        FieldOperator,
        Auditor
    ];
}
