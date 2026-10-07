namespace Scootly.Infrastructure.Authorization;

public static class PolicyNames
{
    /// <summary>Herhangi bir rolde, kullanıcı token'ı taşıyan istekler (cihaz token'ları hariç). Ör. hesap işlemleri.</summary>
    public const string UserOnly = "UserOnly";

    /// <summary>Sürücü rolüne sahip, kullanıcı token'ı taşıyan istekler (cihaz token'ları hariç).</summary>
    public const string DriverOnly = "DriverOnly";

    public const string FleetManagerOnly = "FleetManagerOnly";

    /// <summary>Filo yöneticisi veya saha operatörü: bakım gibi saha operasyonları.</summary>
    public const string FleetOperations = "FleetOperations";

    /// <summary>Yalnızca cihaz (telemetri) token'ları.</summary>
    public const string DeviceOnly = "DeviceOnly";

    /// <summary>Kaynak tabanlı: sürüşün sahibi mi?</summary>
    public const string RideOwner = "RideOwner";
}
