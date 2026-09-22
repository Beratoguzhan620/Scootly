using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Scootly.Application.Abstractions;
using Scootly.Domain.Fleet;

namespace Scootly.Worker.Jobs;

/// <summary>
/// Bataryası eşiğin altına düşen araçları bulur (54. gün).
/// </summary>
/// <remarks>
/// <para>
/// Bugün yalnızca <b>logluyor</b>. Olay yayınlama (RabbitMQ) 13. haftada
/// geliyor; o gelmeden sahte bir kuyruk uydurmak, ileride atılacak bir kod
/// yazmak olurdu. Servisin iskeleti ve tarama sorgusu şimdi kuruluyor, olay
/// yayınlama satırı sonra eklenecek — yer belli ve işaretli.
/// </para>
/// <para>
/// <b>Tekrarlı uyarıyı engelleyen bir durum yok</b>, ve bu bilinçli bir
/// teknik borç: servis her turda aynı araçları tekrar bulur. Gerçek bir
/// sistemde "bu araç için zaten uyarı üretildi" bilgisinin bir yerde durması
/// gerekir (araçta bir işaret ya da ayrı bir uyarı tablosu). Bunu şimdi
/// yapmak, kuyruk gelmeden önce yanlış yere yazmak olurdu.
/// </para>
/// </remarks>
public sealed class BatteryThresholdScanner : PeriyodikServis
{
    /// <summary>Uyarı eşiği.</summary>
    public const int Esik = 20;

    private const int TurBasinaLimit = 500;

    private readonly ILogger<BatteryThresholdScanner> _logger;

    public BatteryThresholdScanner(
        IServiceScopeFactory scopeFactory,
        ILogger<BatteryThresholdScanner> logger)
        : base(scopeFactory, logger)
    {
        _logger = logger;
    }

    protected override TimeSpan Aralik => TimeSpan.FromMinutes(5);

    protected override string Ad => "Batarya esigi taramasi";

    protected override async Task TurAsync(IServiceProvider kapsam, CancellationToken cancellationToken)
    {
        var dbContext = kapsam.GetRequiredService<IApplicationDbContext>();
        var queryExecutor = kapsam.GetRequiredService<IQueryExecutor>();

        var dusukBataryali = await queryExecutor.ToListAsync(
            dbContext.Vehicles
                .Where(v => v.Battery.Percentage < Esik)
                // Bakımdaki araçlar zaten sahada değil; onları raporlamak
                // operatöre yapacak iş değil gürültü üretirdi.
                .Where(v => v.Status != VehicleStatus.Maintenance)
                .OrderBy(v => v.Battery.Percentage)
                .Take(TurBasinaLimit)
                .Select(v => new { v.Id, v.Battery.Percentage }),
            cancellationToken);

        if (dusukBataryali.Count == 0)
        {
            return;
        }

        _logger.LogInformation(
            "{Adet} aracin bataryasi %{Esik} altinda. En dusuk: %{EnDusuk}",
            dusukBataryali.Count,
            Esik,
            dusukBataryali[0].Percentage);

        // 13. HAFTA: burada VehicleBatteryLowIntegrationEvent yayınlanacak.
        foreach (var arac in dusukBataryali)
        {
            _logger.LogDebug("Dusuk batarya: {AracId} (%{Yuzde})", arac.Id, arac.Percentage);
        }
    }
}
