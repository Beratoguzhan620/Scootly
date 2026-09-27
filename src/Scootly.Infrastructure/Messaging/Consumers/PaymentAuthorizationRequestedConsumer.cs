using Scootly.Application.Billing.Commands;
using Scootly.Application.IntegrationEvents;
using Scootly.Domain.Common;

namespace Scootly.Infrastructure.Messaging.Consumers;

/// <summary>Ödeme istendi → sağlayıcıya sor (69. gün — saga'nın 3. adımı).</summary>
public sealed class PaymentAuthorizationRequestedConsumer
    : IIntegrationEventConsumer<PaymentAuthorizationRequestedIntegrationEvent>
{
    private readonly AuthorizeRidePaymentCommandHandler _handler;

    public PaymentAuthorizationRequestedConsumer(AuthorizeRidePaymentCommandHandler handler)
    {
        _handler = handler;
    }

    public static QueueDefinition Queue => RabbitMqTopology.PaymentAuthorization;

    public Task<Result> ConsumeAsync(PaymentAuthorizationRequestedIntegrationEvent integrationEvent, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        return _handler.Handle(
            new AuthorizeRidePaymentCommand(integrationEvent.RideId, integrationEvent.DriverId, integrationEvent.Amount),
            cancellationToken);
    }
}
