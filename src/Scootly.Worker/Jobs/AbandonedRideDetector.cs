using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scootly.Application.Abstractions;
using Scootly.Application.Common;
using Scootly.Domain.Common;
using Scootly.Domain.Fleet;
using Scootly.Domain.Riding;

namespace Scootly.Worker.Jobs;

/// <summary>
/// Uzun süredir açık kalan sürüşleri terk edilmiş olarak kapatır (54. gün).
/// </summary>
/// <remarks>
/// <para>
/// Kullanıcı sürüşü bitirmeyi unutmuş ya da uygulama kapanmış olabilir. Açık
/// kalan sürüş iki şeyi birden bozar: kullanıcı sonsuza kadar ücretlenir ve
/// araç hiç kimsenin kiralayamayacağı bir durumda kalır.
/// </para>
/// <para>
/// <b>Eşik "hareketsizlik" değil, süre.</b> Doğrusu telemetriye bakıp aracın
/// gerçekten hareket etmediğini görmek olurdu — telemetri hattı 51. günde
/// kuruldu ama bu kontrolü ona bağlamak, telemetri kesintisini "sürüş terk
/// edildi"ye çevirirdi. İki saat, kaba ama yan etkisi öngörülebilir bir
/// ölçüt. Telemetri tabanlı hareketsizlik tespiti teknik borç listesinde.
/// </para>
/// <para>
/// Aracın durumu da düzeltiliyor: sürüş kapanıp araç <c>InRide</c> kalsaydı,
/// asıl problem (kiralanamayan araç) çözülmemiş olurdu.
/// </para>
/// </remarks>
public sealed class AbandonedRideDetector : PeriyodikServis
{
    /// <summary>Bu süreden uzun süren aktif sürüşler terk edilmiş sayılır.</summary>
    public static readonly TimeSpan Esik = TimeSpan.FromHours(2);

    private const int TurBasinaLimit = 100;

    private readonly ILogger<AbandonedRideDetector> _logger;

    public AbandonedRideDetector(
        IServiceScopeFactory scopeFactory,
        ILogger<AbandonedRideDetector> logger)
        : base(scopeFactory, logger)
    {
        _logger = logger;
    }

    protected override TimeSpan Aralik => TimeSpan.FromMinutes(10);

    protected override string Ad => "Terk edilmis surus tespiti";

    protected override async Task TurAsync(IServiceProvider kapsam, CancellationToken cancellationToken)
    {
        var dbContext = kapsam.GetRequiredService<IApplicationDbContext>();
        var queryExecutor = kapsam.GetRequiredService<IQueryExecutor>();
        var rides = kapsam.GetRequiredService<IRideRepository>();
        var vehicles = kapsam.GetRequiredService<IVehicleRepository>();
        var unitOfWork = kapsam.GetRequiredService<IUnitOfWork>();
        var clock = kapsam.GetRequiredService<IClock>();
        var nearbyCache = kapsam.GetRequiredService<INearbyVehicleCache>();

        var simdi = clock.UtcNow;
        var sinir = simdi - Esik;

        // ix_rides_durum_bitis indeksi bu sorgu icin var: Status esitlik,
        // ardindan StartedAt araligi. StartedAt'in gercekten bir sutun
        // olmasi, bu sorgunun calismasinin on kosulu — 41. gunde duzeltilen
        // esleme hatasi giderilmeseydi burasi calisma zamaninda patlardi.
        var adaylar = await queryExecutor.ToListAsync(
            dbContext.Rides
                .Where(r => r.Status == RideStatus.Active)
                .Where(r => r.StartedAt < sinir)
                .OrderBy(r => r.StartedAt)
                .Take(TurBasinaLimit)
                .Select(r => r.Id),
            cancellationToken);

        if (adaylar.Count == 0)
        {
            return;
        }

        var kapatilan = 0;

        foreach (var surusId in adaylar)
        {
            var surus = await rides.GetByIdAsync(surusId, cancellationToken);

            if (surus is null || surus.Status != RideStatus.Active)
            {
                continue;
            }

            try
            {
                surus.MarkAbandoned(simdi);

                var arac = await vehicles.GetByIdAsync(surus.VehicleId, cancellationToken);

                if (arac is { Status: VehicleStatus.InRide })
                {
                    arac.CompleteRide();
                }

                // Sürüş ve araç TEK SaveChanges çağrısında: ayrı ayrı
                // kaydedilseydi, sürüşün kapandığı ama aracın hâlâ
                // "sürüşte" olduğu bir an doğardı ve o anda bir hata olursa
                // kalıcı hale gelirdi.
                await unitOfWork.SaveChangesAsync(cancellationToken);
                kapatilan++;
            }
            catch (ConcurrencyConflictException)
            {
                // Kullanıcı tam o anda sürüşü bitirdi. En iyi sonuç.
            }
            catch (DomainException ex)
            {
                _logger.LogWarning(ex, "Surus {SurusId} kapatilamadi.", surusId);
            }
        }

        if (kapatilan > 0)
        {
            await nearbyCache.InvalidateAsync(cancellationToken);

            _logger.LogWarning(
                "{Adet} surus terk edilmis olarak kapatildi (esik: {Esik}).", kapatilan, Esik);
        }
    }
}
