using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Scootly.Worker.Jobs;

/// <summary>
/// Belirli aralıkla çalışan arka plan servisleri için ortak taban (54. gün).
/// </summary>
/// <remarks>
/// <para>
/// Üç servisin de aynı iki tuzağa düşme riski var; taban sınıf ikisini de tek
/// yerde çözüyor:
/// </para>
/// <list type="number">
///   <item>
///   <b>Yakalanmayan istisna servisi sessizce durdurur.</b>
///   <c>ExecuteAsync</c>'ten kaçan bir istisna .NET 6'dan beri varsayılan
///   olarak uygulamayı düşürür; düşürmese bile o servis bir daha hiç
///   çalışmaz ve kimse fark etmez. Burada döngünün gövdesi korunuyor:
///   bir tur hata alırsa loglanıyor ve <b>bir sonraki tur normal
///   çalışıyor</b>.
///   </item>
///   <item>
///   <b>Kapsamlı (scoped) servisi doğrudan enjekte etmek.</b> Arka plan
///   servisi tekil (singleton) ömürlüdür; ona bir <c>DbContext</c>
///   enjekte etmek, uygulama ömrü boyunca yaşayan tek bir bağlam demek —
///   ve <c>DbContext</c> iş parçacığı güvenli değil, değişiklik izleyicisi
///   de sonsuza kadar büyür. Her tur kendi kapsamını açıyor.
///   </item>
/// </list>
/// <para>
/// <c>PeriodicTimer</c> kullanılıyor, <c>Task.Delay</c> döngüsü değil: ikincisi
/// işin süresini aralığa ekler, yani "her 30 saniyede bir" yavaş yavaş "her 35
/// saniyede bir"e kayar.
/// </para>
/// </remarks>
public abstract class PeriyodikServis : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger _logger;

    protected PeriyodikServis(IServiceScopeFactory scopeFactory, ILogger logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    /// <summary>Turlar arası bekleme.</summary>
    protected abstract TimeSpan Aralik { get; }

    /// <summary>Loglarda görünecek ad.</summary>
    protected abstract string Ad { get; }

    /// <summary>Bir turun işi. Kendi kapsamı verilir.</summary>
    protected abstract Task TurAsync(IServiceProvider kapsam, CancellationToken cancellationToken);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("{Servis} basladi, aralik: {Aralik}", Ad, Aralik);

        using var zamanlayici = new PeriodicTimer(Aralik);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await zamanlayici.WaitForNextTickAsync(stoppingToken);

                await using var kapsam = _scopeFactory.CreateAsyncScope();

                await TurAsync(kapsam.ServiceProvider, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal kapanış. Hata olarak loglamak her kapanışta sahte
                // alarm üretirdi.
                break;
            }
            catch (Exception ex)
            {
                // Tur başarısız oldu ama SERVİS DEVAM EDİYOR. Bu satır
                // olmasaydı, geçici bir veritabanı kesintisi rezervasyon
                // zaman aşımlarını kalıcı olarak durdururdu.
                _logger.LogError(ex, "{Servis} turu basarisiz. Bir sonraki turda denenecek.", Ad);
            }
        }

        _logger.LogInformation("{Servis} durdu.", Ad);
    }
}
