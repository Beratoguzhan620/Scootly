using Scootly.Application.IntegrationEvents;

namespace Scootly.Application.Abstractions;

/// <summary>
/// Bir entegrasyon olayını mesaj kuyruğuna gönderir (62. gün).
/// </summary>
/// <remarks>
/// Application katmanı RabbitMQ'yu tanımıyor; yalnızca "bu olayı yayınla"
/// diyor. Yönlendirme anahtarı olayın tipinden geliyor
/// (<see cref="IHasEventName.EventName"/>), çağıran yazmıyor.
/// </remarks>
public interface IEventPublisher
{
    Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken = default)
        where TEvent : IIntegrationEvent, IHasEventName;
}
