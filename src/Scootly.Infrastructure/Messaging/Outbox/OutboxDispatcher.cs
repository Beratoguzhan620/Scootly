using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Scootly.Application.Abstractions;
using Scootly.Infrastructure.Persistence;

namespace Scootly.Infrastructure.Messaging.Outbox;

/// <summary>
/// Outbox'taki bekleyen olayları kuyruğa taşır (67. gün).
/// </summary>
/// <remarks>
/// <para>
/// <b>"Gönderildi" işareti ancak RabbitMQ onayladıktan sonra.</b> Yayınlayıcı
/// yayıncı onayı (publisher confirm) açık çalışıyor; <c>PublishRawAsync</c>
/// döndüğünde mesaj RabbitMQ'nun elinde. Onaydan önce işaretleseydik, arada
/// kopan bir bağlantı olayı sessizce kaybettirirdi.
/// </para>
/// <para>
/// <b>Sonuç: en az bir kez teslim.</b> Olay gönderilip "gönderildi" işareti
/// kaydedilemeden süreç çökerse, bir sonraki turda aynı olay yeniden gider.
/// Bu yüzden tüketicilerin tekrar gelen mesajı tanıması şart (68. gün).
/// "Tam olarak bir kez teslim" iki ayrı sistem arasında kurulamaz; kurulabilen
/// şey "en az bir kez teslim + tekrar kontrolü = etkisi bir kez".
/// </para>
/// <para>
/// <b>İki gönderici aynı anda çalışırsa</b> (Worker iki kopya) ikisi de aynı
/// satırları okuyup ikişer kez gönderirdi. <c>FOR UPDATE SKIP LOCKED</c> bunu
/// engelliyor: bir göndericinin kilitlediği satırları diğeri hiç görmüyor,
/// beklemiyor da — bir sonraki kilitsiz satıra geçiyor.
/// </para>
/// </remarks>
public sealed class OutboxDispatcher
{
    private readonly ScootlyDbContext _dbContext;
    private readonly RabbitMqEventPublisher _publisher;
    private readonly IClock _clock;
    private readonly ILogger<OutboxDispatcher> _logger;

    public OutboxDispatcher(
        ScootlyDbContext dbContext,
        RabbitMqEventPublisher publisher,
        IClock clock,
        ILogger<OutboxDispatcher> logger)
    {
        _dbContext = dbContext;
        _publisher = publisher;
        _clock = clock;
        _logger = logger;
    }

    /// <returns>Bu turda kuyruğa taşınan olay sayısı.</returns>
    public async Task<int> DispatchAsync(int batchSize, CancellationToken cancellationToken = default)
    {
        await using var transaction = await _dbContext.Database.BeginTransactionAsync(cancellationToken);

        var bekleyenler = await _dbContext.OutboxMessages
            .FromSql($"""
                SELECT * FROM "OutboxMessages"
                WHERE "ProcessedAt" IS NULL
                ORDER BY "CreatedAt"
                LIMIT {batchSize}
                FOR UPDATE SKIP LOCKED
                """)
            .ToListAsync(cancellationToken);

        if (bekleyenler.Count == 0)
            return 0;

        var gonderilen = 0;

        foreach (var mesaj in bekleyenler)
        {
            try
            {
                await _publisher.PublishRawAsync(
                    mesaj.EventType, mesaj.Id, Encoding.UTF8.GetBytes(mesaj.Payload), cancellationToken);

                mesaj.MarkProcessed(_clock.UtcNow);
                gonderilen++;
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                mesaj.RecordFailure($"{ex.GetType().Name}: {ex.Message}");

                _logger.LogWarning(ex,
                    "Outbox olayi gonderilemedi: {EventType} {Id} (deneme {Deneme}). Sonraki turda tekrar denenecek.",
                    mesaj.EventType, mesaj.Id, mesaj.Attempts);

                // Baglanti yoksa kalan mesajlari da denemek ayni hatayi N kez
                // almak demek; tur burada bitiyor, basarili olanlar kaydediliyor.
                break;
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return gonderilen;
    }

    /// <summary>
    /// Gönderilmiş outbox kayıtlarını ve eski "işlendi" kayıtlarını siler.
    /// </summary>
    /// <remarks>
    /// Planın "yaygın tuzak" listesindeki ilk madde: outbox'ı temizlememek.
    /// Her sürüş en az üç kayıt üretiyor (sürüş bitti, ödeme istendi, ödeme
    /// sonucu); temizlenmezse tablo sürüş sayısıyla birlikte sınırsız büyür ve
    /// göndericinin sorgusu zamanla yavaşlar.
    /// </remarks>
    public async Task<(int Outbox, int Islenen)> CleanupAsync(
        TimeSpan outboxRetention,
        TimeSpan processedRetention,
        CancellationToken cancellationToken = default)
    {
        var simdi = _clock.UtcNow;
        var outboxSiniri = simdi - outboxRetention;
        var islenenSiniri = simdi - processedRetention;

        var outbox = await _dbContext.OutboxMessages
            .Where(m => m.ProcessedAt != null && m.ProcessedAt < outboxSiniri)
            .ExecuteDeleteAsync(cancellationToken);

        var islenen = await _dbContext.ProcessedMessages
            .Where(m => m.ProcessedAt < islenenSiniri)
            .ExecuteDeleteAsync(cancellationToken);

        return (outbox, islenen);
    }
}
