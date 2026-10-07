namespace Scootly.Infrastructure.Identity;

public static class ScootlyRoles
{
    public const string Driver = "Driver";
    public const string FleetManager = "FleetManager";
    public const string FieldOperator = "FieldOperator";

    /// <summary>Identity kullanıcısı değildir; yalnızca cihaz token'larında bulunur.</summary>
    public const string Device = "Device";

    /// <summary>Kullanıcılara atanabilecek roller (Device hariç).</summary>
    public static readonly IReadOnlyList<string> Assignable = [Driver, FleetManager, FieldOperator];

    // Migration ile tohumlanan rollerin sabit kimlikleri (HasData).
    public static readonly Guid DriverRoleId = new("0b3f5c1e-6a3d-4f2e-9d41-6c2a7e8b1f01");
    public static readonly Guid FleetManagerRoleId = new("0b3f5c1e-6a3d-4f2e-9d41-6c2a7e8b1f02");
    public static readonly Guid FieldOperatorRoleId = new("0b3f5c1e-6a3d-4f2e-9d41-6c2a7e8b1f03");
}

public static class ScootlyClaimTypes
{
    /// <summary>Token'ın bir kullanıcıya mı yoksa bir cihaza mı verildiğini belirtir.</summary>
    public const string ClientType = "client_type";

    public const string UserClient = "user";
    public const string DeviceClient = "device";

    /// <summary>Token verildiği andaki Identity güvenlik damgası; değişirse (rol, parola, silme) token geçersiz olur.</summary>
    public const string SecurityStamp = "sstamp";
}
