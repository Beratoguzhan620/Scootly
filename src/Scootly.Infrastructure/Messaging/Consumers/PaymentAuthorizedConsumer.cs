using Scootly.Application.Billing.Commands;
using Scootly.Application.IntegrationEvents;
using Scootly.Domain.Common;

namespace Scootly.Infrastructure.Messaging.Consumers;

/// <summary>
/// Ödeme sonuçlandı → sürüşü kapat, aracı serbest bırak; başarısızsa telafi et
/// (69. gün — saga'nın son adımı).
/// </summary>
public sealed class PaymentAuthorizedConsumer : IIntegrationEventConsumer<PaymentAuthorizedIntegrationEvent>
{
    private readonly SettleRidePaymentCommandHandler _handler;

    public PaymentAuthorizedConsumer(SettleRidePaymentCommandHandler handler)
    {
        _handler = handler;
    }

    public static QueueDefinition Queue => RabbitMqTopology.PaymentSettlement;

    public Task<Result> ConsumeAsync(PaymentAuthorizedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return _handler.Handle(
            new SettleRidePaymentCommand(integrationEvent.RideId, integrationEvent.Success, integrationEvent.FailureReason),
            cancellationToken);
    }
}
