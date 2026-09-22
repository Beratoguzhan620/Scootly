using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scootly.Application.Abstractions;
using Scootly.Application.Common;
using Scootly.Domain.Common;
using Scootly.Domain.Fleet;

namespace Scootly.Worker.Jobs;

/// <summary>
/// Süresi dolmuş rezervasyonları düşürür (54. gün).
/// </summary>
/// <remarks>
/// <para>
/// Rezervasyon on dakika sürüyor (<c>ReservationPolicy</c>). Süre dolduğunda
/// aracın kendiliğinden serbest kalması gerekiyor; aksi halde bir kullanıcının
/// unuttuğu rezervasyon, aracı sonsuza kadar kimsenin kiralayamayacağı bir
/// duruma sokar.
/// </para>
/// <para>
/// <b>Neden tek bir toplu UPDATE değil.</b> Şöyle yazılabilirdi:
/// <c>UPDATE "Vehicles" SET "Status"='Available' WHERE "Status"='Reserved'
/// AND "ReservedUntil" &lt; now()</c> — tek sorgu, çok daha hızlı. Onu
/// seçmedik: taramanın yaptığı an ile yazmanın yaptığı an arasında kullanıcı
/// aracı kiralamış olabilir. Toplu UPDATE o kiralamayı sessizce ezer ve
/// <b>sürüşteki bir aracı müsait gösterir</b>. Aracı tek tek okuyup
/// kaydetmek, 38. günde eklenen sürüm damgasının devreye girmesini sağlıyor:
/// araya giren bir değişiklik varsa çakışma alınır ve o araç atlanır.
/// </para>
/// <para>
/// Yani buradaki "N+1", performans hatası değil bilinçli bir doğruluk
/// tercihi — ve aday kümesi doğası gereği küçük (aynı anda süresi dolmuş
/// rezervasyon sayısı).
/// </para>
/// </remarks>
public sealed class ReservationTimeoutService : PeriyodikServis
{
    /// <summary>Tek turda en fazla bu kadar araç işlenir.</summary>
    /// <remarks>
    /// Üst sınır olmasaydı, servis uzun süre çalışmadıktan sonraki ilk tur
    /// binlerce aracı tek seferde işlemeye kalkar ve veritabanını bunaltırdı.
    /// Kalanlar bir sonraki turda.
    /// </remarks>
    private const int TurBasinaLimit = 200;

    private readonly ILogger<ReservationTimeoutService> _logger;

    public ReservationTimeoutService(
        IServiceScopeFactory scopeFactory,
        ILogger<ReservationTimeoutService> logger)
        : base(scopeFactory, logger)
    {
        _logger = logger;
    }

    protected override TimeSpan Aralik => TimeSpan.FromSeconds(30);

    protected override string Ad => "Rezervasyon zaman asimi";

    protected override async Task TurAsync(IServiceProvider kapsam, CancellationToken cancellationToken)
    {
        var dbContext = kapsam.GetRequiredService<IApplicationDbContext>();
        var queryExecutor = kapsam.GetRequiredService<IQueryExecutor>();
        var repository = kapsam.GetRequiredService<IVehicleRepository>();
        var unitOfWork = kapsam.GetRequiredService<IUnitOfWork>();
        var clock = kapsam.GetRequiredService<IClock>();
        var nearbyCache = kapsam.GetRequiredService<INearbyVehicleCache>();

        var simdi = clock.UtcNow;

        // Aday listesi TAKİPSİZ okunuyor (varsayılan) ve yalnızca kimlikler
        // çekiliyor: bütün aracı yüklemenin anlamı yok, hemen ardından
        // tekrar okunacaklar.
        var adaylar = await queryExecutor.ToListAsync(
            dbContext.Vehicles
                .Where(v => v.Status == VehicleStatus.Reserved)
                .Where(v => v.ReservedUntil != null && v.ReservedUntil < simdi)
                .OrderBy(v => v.ReservedUntil)
                .Take(TurBasinaLimit)
                .Select(v => v.Id),
            cancellationToken);

        if (adaylar.Count == 0)
        {
            return;
        }

        var dusurulen = 0;
        var atlanan = 0;

        foreach (var aracId in adaylar)
        {
            var arac = await repository.GetByIdAsync(aracId, cancellationToken);

            if (arac is null || arac.Status != VehicleStatus.Reserved)
            {
                // Araya giren bir değişiklik olmuş: araç kiralanmış ya da
                // silinmiş. Atlıyoruz.
                atlanan++;
                continue;
            }

            try
            {
                arac.ReleaseReservation();
                await unitOfWork.SaveChangesAsync(cancellationToken);
                dusurulen++;
            }
            catch (ConcurrencyConflictException)
            {
                // Okuduktan sonra, yazmadan önce biri aracı kiraladı.
                // Sürüm damgası bunu yakaladı — toplu UPDATE yakalayamazdı.
                atlanan++;
            }
            catch (DomainException)
            {
                atlanan++;
            }
        }

        if (dusurulen > 0)
        {
            // Araçlar tekrar müsait; harita önbelleği yanlış.
            await nearbyCache.InvalidateAsync(cancellationToken);

            _logger.LogInformation(
                "{Dusurulen} rezervasyon dusuruldu, {Atlanan} arac atlandi.", dusurulen, atlanan);
        }
    }
}
