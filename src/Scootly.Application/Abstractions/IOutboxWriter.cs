using Scootly.Application.IntegrationEvents;

namespace Scootly.Application.Abstractions;

/// <summary>
/// Olayı kuyruğa değil, veritabanındaki outbox tablosuna yazar (66. gün).
/// </summary>
/// <remarks>
/// <para>
/// Yazılan kayıt HENÜZ kaydedilmedi: aynı iş birimine (<see cref="IUnitOfWork"/>)
/// ekleniyor ve çağıranın <c>SaveChangesAsync</c>'i ile, çağıranın yaptığı
/// değişikliklerle AYNI transaction'da veritabanına gidiyor. Ya ikisi birden
/// yazılır ya hiçbiri — ikili yazma probleminin çözümü tam olarak bu.
/// </para>
/// <para>
/// Kuyruğa taşımak ayrı bir işin (Worker'daki outbox göndericisi) görevi.
/// </para>
/// </remarks>
public interface IOutboxWriter
{
    Task WriteAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent, IHasEventName;
}
