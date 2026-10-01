using Scootly.Application.Abstractions;
using Scootly.Application.Common;
using Scootly.Domain.Common;
using Scootly.Domain.FieldOps;

namespace Scootly.Application.FieldOps.Commands;

public sealed record CreateFieldTaskCommand(Guid VehicleId, FieldTaskType Type);

public sealed record AssignFieldTaskCommand(Guid FieldTaskId, Guid OperatorId);

public sealed record CompleteFieldTaskCommand(Guid FieldTaskId, Guid OperatorId, string? Note);

/// <summary>Saha görevi yaşam döngüsü: oluşturma, üstlenme, tamamlama.</summary>
public sealed class FieldTaskCommandHandler
{
    private readonly IFieldTaskRepository _fieldTaskRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public FieldTaskCommandHandler(IFieldTaskRepository fieldTaskRepository, IUnitOfWork unitOfWork, IClock clock)
    {
        _fieldTaskRepository = fieldTaskRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result<Guid>> Handle(CreateFieldTaskCommand command, CancellationToken cancellationToken = default)
    {
        var hasOpenTask = await _fieldTaskRepository.HasOpenTaskAsync(command.VehicleId, command.Type, cancellationToken);

        if (hasOpenTask)
            return Result<Guid>.Success(Guid.Empty); // Aynı araç+tür için zaten açık görev var; yenisi açılmaz.

        var fieldTask = new FieldTask(Guid.NewGuid(), command.VehicleId, command.Type, _clock.UtcNow);

        await _fieldTaskRepository.AddAsync(fieldTask, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result<Guid>.Success(fieldTask.Id);
    }

    public Task<Result> Handle(AssignFieldTaskCommand command, CancellationToken cancellationToken = default)
    {
        return OptimisticConcurrency.ExecuteAsync(_unitOfWork, async token =>
        {
            var fieldTask = await _fieldTaskRepository.GetByIdAsync(command.FieldTaskId, token);

            if (fieldTask is null)
                return Result.NotFound("Görev bulunamadı.");

            try
            {
                fieldTask.Assign(command.OperatorId, _clock.UtcNow);
            }
            catch (DomainException ex)
            {
                return Result.Validation(ex.Message);
            }

            await _unitOfWork.SaveChangesAsync(token);

            return Result.Success();
        },
        conflictMessage: "Görev bu sırada değişti. Lütfen tekrar deneyin.",
        cancellationToken);
    }

    public Task<Result> Handle(CompleteFieldTaskCommand command, CancellationToken cancellationToken = default)
    {
        return OptimisticConcurrency.ExecuteAsync(_unitOfWork, async token =>
        {
            var fieldTask = await _fieldTaskRepository.GetByIdAsync(command.FieldTaskId, token);

            if (fieldTask is null)
                return Result.NotFound("Görev bulunamadı.");

            try
            {
                fieldTask.Complete(command.OperatorId, _clock.UtcNow, command.Note);
            }
            catch (DomainException ex)
            {
                return Result.Validation(ex.Message);
            }

            await _unitOfWork.SaveChangesAsync(token);

            return Result.Success();
        },
        conflictMessage: "Görev bu sırada değişti. Lütfen tekrar deneyin.",
        cancellationToken);
    }
}