using Microsoft.Extensions.Logging;
using Scootly.Application.Abstractions;
using Scootly.Domain.Common;

namespace Scootly.Application.Behaviors;

public sealed class TransactionBehavior<TCommand>
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<TransactionBehavior<TCommand>> _logger;

    public TransactionBehavior(IUnitOfWork unitOfWork, ILogger<TransactionBehavior<TCommand>> logger)
    {
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<Result> ExecuteAsync(
        TCommand command,
        Func<TCommand, CancellationToken, Task<Result>> handlerAction,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Transaction başlıyor: {CommandType}", typeof(TCommand).Name);

        var result = await handlerAction(command, cancellationToken);

        if (result.IsSuccess)
        {
            _logger.LogInformation("Transaction başarılı: {CommandType}", typeof(TCommand).Name);
        }
        else
        {
            _logger.LogWarning("Transaction başarısız: {CommandType} — {Error}", typeof(TCommand).Name, result.Error);
        }

        return result;
    }
}