namespace Scootly.Application.Abstractions;

public interface IEventPublisher
{
    Task PublishAsync<T>(T integrationEvent, CancellationToken cancellationToken = default);
}