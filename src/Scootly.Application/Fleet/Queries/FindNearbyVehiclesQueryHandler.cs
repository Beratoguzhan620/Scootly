using Scootly.Application.Abstractions;
using Scootly.Application.Common;
using Scootly.Domain.Fleet;
using Scootly.Domain.Geo;

namespace Scootly.Application.Fleet.Queries;

/// <summary>
/// Yakındaki müsait araçları sayfalı olarak döndürür.
/// </summary>
/// <remarks>
/// <para>
/// Bu handler Faz 3'ün üç gününü birden taşıyor:
/// </para>
/// <list type="bullet">
///   <item><b>41. gün:</b> sorgu takipsiz çalışıyor. Ama burada
///   <c>AsNoTracking()</c> yazmıyor — takipsizlik artık
///   <see cref="Scootly.Infrastructure.Persistence.ScootlyDbContext"/>'in
///   varsayılanı, yazma yolları açıkça <c>AsTracking()</c> diyor. Gerekçe
///   ADR 0016'da: güvenli olanı varsayılan yapmak, her okuma sorgusuna bir
///   satır eklemeyi hatırlamaktan daha dayanıklı.</item>
///   <item><b>42. gün:</b> toplam kök değil, dört alanlı bir DTO'ya
///   projeksiyon.</item>
///   <item><b>43. gün:</b> yarıçap filtresi veritabanında. Bellekte
///   filtrelenseydi PostgreSQL tüm satırları ağdan geçirirdi.</item>
/// </list>
/// <para>
/// 48. gün önbelleği de burada devrede: cache-aside deseninde önbellek,
/// veritabanının önünde <b>pasif</b> bir katman — yani veritabanını önbellek
/// çağırmıyor, uygulama önce önbelleğe bakıp bulamazsa veritabanına gidiyor.
/// Bunun görünür sonucu şu: Redis çökse bile bu uç çalışmaya devam eder,
/// yalnızca yavaşlar.
/// </para>
/// </remarks>
public sealed class FindNearbyVehiclesQueryHandler
{
    private readonly IApplicationDbContext _dbContext;
    private readonly IQueryExecutor _queryExecutor;
    private readonly INearbyVehicleCache _cache;

    public FindNearbyVehiclesQueryHandler(
        IApplicationDbContext dbContext,
        IQueryExecutor queryExecutor,
        INearbyVehicleCache cache)
    {
        _dbContext = dbContext;
        _queryExecutor = queryExecutor;
        _cache = cache;
    }

    public Task<PagedResult<NearbyVehicleDto>> Handle(
        FindNearbyVehiclesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        return _cache.GetOrSetAsync(query, ct => VeritabanindanAsync(query, ct), cancellationToken);
    }

    /// <summary>Önbelleği atlayarak doğrudan veritabanından okur (ıska yolu).</summary>
    public async Task<PagedResult<NearbyVehicleDto>> VeritabanindanAsync(
        FindNearbyVehiclesQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var yaricap = Math.Clamp(query.RadiusMeters, 1d, FindNearbyVehiclesQuery.MaxRadiusMeters);
        var kutu = BoundingBox.Around(new GeoPoint(query.Latitude, query.Longitude), yaricap);

        // Sınırlar yerel değişkenlere alınıyor. EF bunları sorgu parametresi
        // olarak gönderiyor; nesne üzerinden erişilseydi de çevirirdi, ama
        // yerel değişken üretilen SQL'i okurken neyin parametre olduğunu
        // gözle görülür kılıyor.
        var minEnlem = kutu.MinLatitude;
        var maxEnlem = kutu.MaxLatitude;
        var minBoylam = kutu.MinLongitude;
        var maxBoylam = kutu.MaxLongitude;

        var sorgu = _dbContext.Vehicles
            // Kısmi indeks (ix_vehicles_durum) tam olarak bu koşul için var.
            .Where(v => v.Status == VehicleStatus.Available)
            .Where(v => v.Location.Latitude >= minEnlem && v.Location.Latitude <= maxEnlem)
            .Where(v => v.Location.Longitude >= minBoylam && v.Location.Longitude <= maxBoylam);

        // COUNT, sayfadan ayrı ikinci bir sorgu. Bedeli PagedResult'ın
        // belgesinde yazılı; burada ölçülüp 43. günün tablosuna kaydedildi.
        var toplam = await _queryExecutor.CountAsync(sorgu, cancellationToken);

        var kayitlar = await _queryExecutor.ToListAsync(
            sorgu
                // Sıralama olmadan sayfalama bozuktur: PostgreSQL satır
                // düzenini garanti etmez, aynı kayıt iki sayfada birden
                // çıkabilir ve bu veri azken fark edilmez.
                .OrderBy(v => v.Id)
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(v => new NearbyVehicleDto(
                    v.Id,
                    v.Location.Latitude,
                    v.Location.Longitude,
                    v.Battery.Percentage)),
            cancellationToken);

        return new PagedResult<NearbyVehicleDto>(kayitlar, query.PageNumber, query.PageSize, toplam);
    }
}
