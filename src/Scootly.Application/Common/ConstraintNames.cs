namespace Scootly.Application.Common;

/// <summary>
/// İş kurallarını veritabanı düzeyinde de garanti eden benzersizlik kısıtları.
/// Infrastructure bu adlarla indeks oluşturur; Application ihlalleri kullanıcı mesajına çevirir.
/// </summary>
public static class ConstraintNames
{
    public const string OneActiveReservationPerDriver = "IX_Vehicles_ReservedBy";
    public const string OneActiveRidePerDriver = "IX_Rides_DriverId_Active";
    public const string OneActiveRidePerVehicle = "IX_Rides_VehicleId_Active";
    public const string UniqueServiceAreaName = "IX_ServiceAreas_Name";
    public const string OneOpenFieldTaskPerVehicleAndType = "IX_FieldTasks_OneOpenTaskPerVehicleAndType";

    public static string ToUserMessage(string? constraintName) => constraintName switch
    {
        OneActiveReservationPerDriver => "Zaten aktif bir rezervasyonunuz var.",
        OneActiveRidePerDriver => "Zaten devam eden bir sürüşünüz var.",
        OneActiveRidePerVehicle => "Bu araçta zaten devam eden bir sürüş var.",
        UniqueServiceAreaName => "Bu adla bir hizmet bölgesi zaten var.",
        OneOpenFieldTaskPerVehicleAndType => "Bu araç için aynı türde açık bir görev zaten var.",
        _ => "İşlem, mevcut bir kayıtla çakıştı."
    };
}
