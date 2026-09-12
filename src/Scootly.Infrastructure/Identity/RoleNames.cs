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
    /// Araç cihazı (25. gün).
    /// </summary>
    /// <remarks>
    /// Bu bir Identity rolü DEĞİL ve bilerek <see cref="All"/> listesinde yok.
    /// Cihazlar kullanıcı değildir: parolaları, e-postaları, hesap kilitleri
    /// yoktur. Rol yalnızca cihaz token'ının içinde bir iddia olarak taşınır.
    /// Identity tablolarına bir "cihaz rolü" satırı eklemek, bir kullanıcıya
    /// yanlışlıkla o rolün atanabilmesi demek olurdu.
    /// </remarks>
    public const string VehicleDevice = "VehicleDevice";

    /// <summary>
    /// Tohumlama (seed) ve testlerin üzerinden geçtiği tam liste.
    /// Ziyaretçi kimliksiz kullanıcıdır; araç cihazı ayrı bir token akışıyla
    /// gelir — ikisi de burada yok.
    /// </summary>
    public static readonly IReadOnlyList<string> All =
    [
        Driver,
        FleetManager,
        FieldOperator,
        Auditor
    ];
}
