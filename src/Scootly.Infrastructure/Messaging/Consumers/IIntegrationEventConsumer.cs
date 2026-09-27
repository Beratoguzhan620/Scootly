using Scootly.Application.IntegrationEvents;
using Scootly.Domain.Common;

namespace Scootly.Infrastructure.Messaging.Consumers;

/// <summary>
/// Bir entegrasyon olayını işleyen tüketici (64. gün).
/// </summary>
/// <remarks>
/// <para>
/// Tüketici yalnızca bir çevirmen: mesajı bir komuta çevirir, Application'daki
/// handler'ı çağırır. İş kuralı burada değil. Kuyruğu dinlemek, onaylamak
/// (ack), yeniden denemek ve ölü mektup kuyruğuna taşımak ise Worker'daki
/// <c>RabbitMqConsumerService</c>'in işi — her tüketici bunları kendisi
/// yazsaydı ikinci tüketicide biri unutulurdu.
/// </para>
/// <para>
/// <b>Dönüş değerinin anlamı:</b> <see cref="Result.Failure(string)"/> KALICI bir
/// ret demek (sürüş yok, veri geçersiz) — mesaj yeniden denenmeden ölü mektup
/// kuyruğuna gider. İstisna ise GEÇİCİ bir hata sayılır (veritabanı kısa
/// süreliğine yok) ve mesaj birkaç saniye sonra yeniden denenir.
/// </para>
/// </remarks>
public interface IIntegrationEventConsumer<TEvent>
    where TEvent : IIntegrationEvent
{
    /// <summary>Tüketicinin dinlediği kuyruk (bkz. <see cref="RabbitMqTopology"/>).</summary>
    static abstract QueueDefinition Queue { get; }

    Task<Result> ConsumeAsync(TEvent integrationEvent, CancellationToken cancellationToken);
}
