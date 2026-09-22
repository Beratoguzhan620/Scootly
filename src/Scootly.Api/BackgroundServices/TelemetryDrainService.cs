using System.Diagnostics;
using Scootly.Application.Abstractions;
using Scootly.Application.Telemetry;
using Scootly.Domain.Telemetry;

namespace Scootly.Api.BackgroundServices;

/// <summary>
/// Telemetri kuyruğunu boşaltıp toplu olarak veritabanına yazar (52+54. gün).
/// </summary>
/// <remarks>
/// <para>
/// Kanalın tüketici tarafı. API isteği kuyruğa yazıp hemen dönüyor, bu servis
/// kendi hızında okuyup yazıyor — ikisinin hızı birbirinden bağımsız.
/// </para>
/// <para>
/// <b>Gruplama (batching) burada yapılıyor.</b> Kuyruktan okunan her kayıt tek
/// tek yazılsaydı kanal hiçbir şey kazandırmazdı: yazma yine satır başına bir
/// gidiş dönüş olurdu. Servis ya <see cref="MaxGrup"/> kayıt birikene ya da
/// <see cref="MaxBekleme"/> geçene kadar topluyor — ikinci koşul önemli, yoksa
/// trafiğin düşük olduğu saatlerde son birkaç kayıt kuyrukta süresiz beklerdi.
/// </para>
/// <para>
/// <b>Yakalanmayan istisna arka plan servisini sessizce durdurur</b> — 55.
/// günün "yaygın tuzaklar" listesindeki ikinci madde. <c>ExecuteAsync</c>'ten
/// kaçan bir istisna, .NET 6'dan beri varsayılan olarak uygulamayı düşürür;
/// düşürmese bile telemetri yazımı durur ve kimse fark etmez. Bu yüzden
/// döngünün gövdesi baştan sona korunuyor ve hata durumunda servis DEVAM
/// ediyor.
/// </para>
/// <para>
/// <b>Kapsamlı (scoped) servis doğrudan enjekte EDİLMİYOR</b> — aynı listenin
/// birinci maddesi. <see cref="ITelemetryWriter"/> kendi bağlantısını açan
/// tekil (singleton) bir bileşen; <c>DbContext</c> olsaydı, uygulama ömrü
/// boyunca yaşayan tek bir bağlam iş parçacığı güvenli olmadığı için bozulurdu.
/// </para>
/// </remarks>
public sealed class TelemetryDrainService : BackgroundService
{
    /// <summary>Tek yazımda gönderilecek en fazla kayıt.</summary>
    public const int MaxGrup = 1_000;

    /// <summary>Grup dolmasa bile en fazla bu kadar beklenir.</summary>
    public static readonly TimeSpan MaxBekleme = TimeSpan.FromSeconds(2);

    private readonly TelemetryChannel _channel;
    private readonly ITelemetryWriter _writer;
    private readonly ILogger<TelemetryDrainService> _logger;

    public TelemetryDrainService(
        TelemetryChannel channel,
        ITelemetryWriter writer,
        ILogger<TelemetryDrainService> logger)
    {
        _channel = channel;
        _writer = writer;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Telemetri bosaltma servisi basladi.");

        var grup = new List<TelemetryReading>(MaxGrup);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await GrupTopla(grup, stoppingToken);

                if (grup.Count == 0)
                {
                    continue;
                }

                var kronometre = Stopwatch.StartNew();
                var yazilan = await _writer.WriteBatchAsync(grup, stoppingToken);
                kronometre.Stop();

                _logger.LogDebug(
                    "{Yazilan} telemetri kaydi {Sure} ms'de yazildi.",
                    yazilan, kronometre.ElapsedMilliseconds);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                // Normal kapanış. Bu istisnayı hata olarak loglamak, her
                // kapanışta sahte bir alarm üretirdi.
                break;
            }
            catch (Exception ex)
            {
                // Yazma başarısız: bu grup KAYBEDİLDİ. Yeniden denemiyoruz,
                // çünkü aynı hata tekrarlarsa (örneğin veritabanı kapalı)
                // sonsuz döngüye gireriz ve kuyruk dolup daha çok kayıt
                // atılır. Telemetri kayıp toleranslı — ama kayıp SESSİZ
                // olmamalı, bu yüzden Error seviyesinde loglanıyor.
                _logger.LogError(
                    ex, "Telemetri grubu YAZILAMADI, {Adet} kayit kaybedildi.", grup.Count);
            }
            finally
            {
                grup.Clear();
            }
        }

        _logger.LogInformation(
            "Telemetri bosaltma servisi durdu. Toplam atilan kayit: {Atilan}",
            _channel.AtilanKayitSayisi);
    }

    /// <summary>Grup dolana ya da süre dolana kadar kuyruktan okur.</summary>
    private async Task GrupTopla(List<TelemetryReading> grup, CancellationToken stoppingToken)
    {
        // İlk kayıt için süresiz bekleniyor: kuyruk boşken meşgul döngü
        // (busy loop) kurmak bir çekirdeği boşuna harcardı.
        if (!await _channel.Okuyucu.WaitToReadAsync(stoppingToken))
        {
            return;
        }

        using var pencere = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        pencere.CancelAfter(MaxBekleme);

        try
        {
            while (grup.Count < MaxGrup && _channel.Okuyucu.TryRead(out var okuma))
            {
                grup.Add(okuma);
            }

            // Grup dolmadıysa, pencere kapanana kadar biraz daha bekle.
            while (grup.Count < MaxGrup && await _channel.Okuyucu.WaitToReadAsync(pencere.Token))
            {
                while (grup.Count < MaxGrup && _channel.Okuyucu.TryRead(out var okuma))
                {
                    grup.Add(okuma);
                }
            }
        }
        catch (OperationCanceledException) when (!stoppingToken.IsCancellationRequested)
        {
            // Bekleme penceresi doldu — hata değil, elimizdekini yaz demek.
        }
    }
}
