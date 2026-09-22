namespace Scootly.Application.Fleet.Queries;

/// <summary>
/// Harita ekranının bir araç için ihtiyaç duyduğu alanların tamamı.
/// </summary>
/// <remarks>
/// <para>
/// 42. günün çıktısı. Önceki hal <c>Vehicle</c> toplam kökünü (aggregate)
/// olduğu gibi yüklüyordu: Model (Brand, RangeKm), Battery, Location, Status,
/// sürüm damgası ve her satır için bir değişiklik takip girdisi. Harita
/// ekranının bunların yarısına ihtiyacı yok.
/// </para>
/// <para>
/// Projeksiyon yalnızca ağ trafiğini azaltmıyor. <c>Select</c> ile doğrudan bu
/// tipe yazıldığında EF Core üretilen SQL'de gerçekten yalnızca dört sütun
/// seçiyor; toplam kökü yükleseydi sahip olunan tipleri (owned types) de
/// doldurmak zorunda kalırdı. N+1'in bu projedeki hali de buydu: her aracın
/// sahip olunan tipleri ayrı ayrı gezilirken sorgu sayısı satır sayısıyla
/// birlikte büyüyordu.
/// </para>
/// <para>
/// Bu tip Application katmanında, Api'nin sözleşmesinde DEĞİL. İkisi ayrı
/// şeyler: bu, bir sorgunun sonucu; oradaki, bir HTTP yanıtının şekli. Bugün
/// alanları aynı, ama API sürümlendiğinde (30. gün) yanıt değişip sorgu aynı
/// kalabilmeli.
/// </para>
/// </remarks>
public sealed record NearbyVehicleDto(
    Guid Id,
    double Latitude,
    double Longitude,
    int BatteryPercentage);
