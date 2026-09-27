using Scootly.Infrastructure.Messaging.Outbox;

namespace Scootly.Worker.Jobs;

/// <summary>
/// Outbox'ı iki saniyede bir kuyruğa boşaltır, saatte bir temizler (67. gün).
/// </summary>
/// <remarks>
/// <para>
/// Worker'da, API'de değil: API yatay ölçeklendiğinde her kopya kendi
/// göndericisini çalıştırırdı. <c>SKIP LOCKED</c> sayesinde bu doğruluk sorunu
/// olmazdı ama gereksiz bağlantı ve sorgu yükü olurdu.
/// </para>
/// <para>
/// Dolu bir turdan sonra beklemeden devam ediyor: 500 bekleyen olay varken
/// her 50'lik grup arasında iki saniye beklemek, birikmiş kuyruğu 20 saniyede
/// boşaltmak yerine bir saniyede boşaltmamayı seçmek olurdu.
/// </para>
/// </remarks>
public sealed class OutboxDispatcherService : BackgroundService
{
    private const int GrupBoyutu = 50;
    private static readonly TimeSpan BosTurBeklemesi = TimeSpan.FromSeconds(2);
    private static readonly TimeSpan TemizlikAraligi = TimeSpan.FromHours(1);
    private static readonly TimeSpan OutboxSaklama = TimeSpan.FromDays(7);
    private static readonly TimeSpan IslenenSaklama = TimeSpan.FromDays(14);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<OutboxDispatcherService> _logger;
    private DateTime _sonTemizlik = DateTime.MinValue;

    public OutboxDispatcherService(IServiceScopeFactory scopeFactory, ILogger<OutboxDispatcherService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Outbox gondericisi basladi ({Saniye} sn aralikla).", BosTurBeklemesi.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            var gonderilen = 0;

            try
            {
                await using var kapsam = _scopeFactory.CreateAsyncScope();
                var gonderici = kapsam.ServiceProvider.GetRequiredService<OutboxDispatcher>();

                gonderilen = await gonderici.DispatchAsync(GrupBoyutu, stoppingToken);

                if (gonderilen > 0)
                    _logger.LogInformation("Outbox: {Adet} olay kuyruga tasindi.", gonderilen);

                if (DateTime.UtcNow - _sonTemizlik > TemizlikAraligi)
                {
                    var (outbox, islenen) = await gonderici.CleanupAsync(OutboxSaklama, IslenenSaklama, stoppingToken);
                    _sonTemizlik = DateTime.UtcNow;

                    if (outbox + islenen > 0)
                        _logger.LogInformation("Outbox temizligi: {Outbox} gonderilmis olay, {Islenen} eski islendi kaydi silindi.", outbox, islenen);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Outbox turu basarisiz; bir sonraki turda tekrar denenecek.");
            }

            if (gonderilen >= GrupBoyutu)
                continue;

            try
            {
                await Task.Delay(BosTurBeklemesi, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
