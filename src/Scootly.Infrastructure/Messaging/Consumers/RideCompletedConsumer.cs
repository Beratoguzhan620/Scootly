using Scootly.Application.IntegrationEvents;
using Scootly.Application.Pricing.Commands;
using Scootly.Domain.Common;

namespace Scootly.Infrastructure.Messaging.Consumers;

/// <summary>
/// Sürüş bitti → ücreti hesapla (64. gün).
/// </summary>
/// <remarks>
/// Ücret hesabı artık sürüş bitirme isteğinin parçası değil. İsteğin yanıt
/// süresi kısalıyor, ve ücret hesabında bir sorun olsa bile kullanıcı "sürüş
/// bitti" onayını alıyor; hesap arka planda, gerekirse yeniden denenerek
/// tamamlanıyor.
/// </remarks>
public sealed class RideCompletedConsumer : IIntegrationEventConsumer<RideCompletedIntegrationEvent>
{
    private readonly ApplyRideFareCommandHandler _handler;

    public RideCompletedConsumer(ApplyRideFareCommandHandler handler)
    {
        _handler = handler;
    }

    public static QueueDefinition Queue => RabbitMqTopology.FareCalculation;

    public Task<Result> ConsumeAsync(RideCompletedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return _handler.Handle(new ApplyRideFareCommand(integrationEvent.RideId), cancellationToken);
    }
}
