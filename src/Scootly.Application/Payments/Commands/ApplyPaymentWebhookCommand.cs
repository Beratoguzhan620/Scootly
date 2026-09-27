using Scootly.Application.Abstractions;
using Scootly.Application.Common;
using Scootly.Domain.Common;

namespace Scootly.Application.Payments.Commands;

/// <summary>Ödeme sağlayıcısının imzası doğrulanmış bildirimi.</summary>
public sealed record ApplyPaymentWebhookCommand(Guid RideId, bool Success, string Message);

public sealed class ApplyPaymentWebhookCommandHandler
{
    private readonly IRideRepository _rideRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public ApplyPaymentWebhookCommandHandler(IRideRepository rideRepository, IUnitOfWork unitOfWork, IClock clock)
    {
        _rideRepository = rideRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    /// <remarks>
    /// Webhook, onaylar için yetkili kaynaktır (senkron yanıt zaman aşımına uğramış olsa bile ödeme alınmış olabilir).
    /// Retler senkron akışta zaten deneme olarak sayıldığından burada ikinci kez sayılmaz.
    /// </remarks>
    public Task<Result> Handle(ApplyPaymentWebhookCommand command, CancellationToken cancellationToken = default)
    {
        return OptimisticConcurrency.ExecuteAsync(_unitOfWork, async token =>
        {
            var ride = await _rideRepository.GetByIdAsync(command.RideId, token);

            if (ride is null)
                return Result.NotFound("Sürüş bulunamadı.");

            if (!command.Success)
                return Result.Success();

            ride.RecordPaymentApproved(_clock.UtcNow);
            await _unitOfWork.SaveChangesAsync(token);

            return Result.Success();
        },
        conflictMessage: "Sürüşün ödeme durumu bu sırada değişti.",
        cancellationToken);
    }
}
