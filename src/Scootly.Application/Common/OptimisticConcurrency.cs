using Scootly.Application.Abstractions;
using Scootly.Application.Abstractions.Exceptions;
using Scootly.Domain.Common;

namespace Scootly.Application.Common;

/// <summary>
/// Yazma işlemlerinin ortak hata disiplini:
/// eşzamanlılık çakışmasında taze veriyle yeniden dener (telemetri güncellemeleri gibi zararsız yarışlar için),
/// iş kuralı ihlallerini ve benzersizlik ihlallerini <see cref="Result"/>'a çevirir.
/// </summary>
public static class OptimisticConcurrency
{
    public const int MaxAttempts = 3;

    public static Task<Result> ExecuteAsync(
        IUnitOfWork unitOfWork,
        Func<CancellationToken, Task<Result>> operation,
        string conflictMessage,
        CancellationToken cancellationToken)
        => ExecuteCoreAsync(unitOfWork, operation, message => Result.Failure(message), conflictMessage, cancellationToken);

    public static Task<Result<T>> ExecuteAsync<T>(
        IUnitOfWork unitOfWork,
        Func<CancellationToken, Task<Result<T>>> operation,
        string conflictMessage,
        CancellationToken cancellationToken)
        => ExecuteCoreAsync(unitOfWork, operation, message => Result<T>.Failure(message), conflictMessage, cancellationToken);

    private static async Task<TResult> ExecuteCoreAsync<TResult>(
        IUnitOfWork unitOfWork,
        Func<CancellationToken, Task<TResult>> operation,
        Func<string, TResult> conflict,
        string conflictMessage,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await operation(cancellationToken);
            }
            catch (DomainException ex)
            {
                unitOfWork.DiscardChanges();
                return conflict(ex.Message);
            }
            catch (UniqueConstraintViolationException ex)
            {
                unitOfWork.DiscardChanges();
                return conflict(ConstraintNames.ToUserMessage(ex.ConstraintName));
            }
            catch (ConcurrencyConflictException) when (attempt < MaxAttempts)
            {
                unitOfWork.DiscardChanges();
            }
            catch (ConcurrencyConflictException)
            {
                unitOfWork.DiscardChanges();
                return conflict(conflictMessage);
            }
        }
    }
}
