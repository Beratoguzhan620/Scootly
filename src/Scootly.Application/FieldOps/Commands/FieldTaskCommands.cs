using Scootly.Application.Abstractions;
using Scootly.Application.Common;
using Scootly.Domain.Common;
using Scootly.Domain.FieldOps;

namespace Scootly.Application.FieldOps.Commands;

public sealed record CreateFieldTaskCommand(Guid VehicleId, FieldTaskType Type);

public sealed record AssignFieldTaskCommand(Guid FieldTaskId, Guid OperatorId);

public sealed record CompleteFieldTaskCommand(Guid FieldTaskId, Guid OperatorId, string? Note, byte[]? PhotoContent = null);

/// <summary>Saha görevi yaşam döngüsü: oluşturma, üstlenme, tamamlama.</summary>
public sealed class FieldTaskCommandHandler
{
    private readonly IFieldTaskRepository _fieldTaskRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;
    private readonly IFileStorage _fileStorage;

    public FieldTaskCommandHandler(IFieldTaskRepository fieldTaskRepository, IUnitOfWork unitOfWork, IClock clock, IFileStorage fileStorage)
    {
        _fieldTaskRepository = fieldTaskRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
        _fileStorage = fileStorage;
    }

    private async Task DeleteQuietlyAsync(string objectKey)
    {
        try
        {
            await _fileStorage.DeleteAsync(objectKey, CancellationToken.None);
        }
        catch (Exception)
        {
            // En iyi caba temizlik: yukleme basarili ama kayit basarisiz olursa yetim nesne kalabilir (teknik borc).
        }
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
        FieldTaskPhotoInfo? photoInfo = null;

        if (command.PhotoContent is not null)
        {
            if (!_fileStorage.IsEnabled)
                return Task.FromResult(Result.Validation("Foto\u011fraf depolama kapal\u0131."));

            var inspected = FieldTaskPhotoRules.Inspect(command.PhotoContent);

            if (!inspected.IsSuccess)
                return Task.FromResult(Result.Validation(inspected.Error));

            photoInfo = inspected.Value;
        }

        return OptimisticConcurrency.ExecuteAsync(_unitOfWork, async token =>
        {
            var fieldTask = await _fieldTaskRepository.GetByIdAsync(command.FieldTaskId, token);

            if (fieldTask is null)
                return Result.NotFound("Görev bulunamadı.");

            // Nesne adi sunucuda uretilir; kullanicinin dosya adi hicbir yerde kullanilmaz.
            string? photoKey = photoInfo is null
                ? null
                : $"field-tasks/{fieldTask.Id:N}/{Guid.NewGuid():N}{photoInfo.Extension}";

            try
            {
                fieldTask.Complete(command.OperatorId, _clock.UtcNow, command.Note, photoKey);
            }
            catch (DomainException ex)
            {
                return Result.Validation(ex.Message);
            }

            if (photoKey is not null)
            {
                try
                {
                    await _fileStorage.PutAsync(photoKey, command.PhotoContent!, photoInfo!.ContentType, token);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _unitOfWork.DiscardChanges();
                    return Result.Failure("Foto\u011fraf y\u00fcklenemedi. L\u00fctfen tekrar deneyin.");
                }
            }

            try
            {
                await _unitOfWork.SaveChangesAsync(token);
            }
            catch
            {
                if (photoKey is not null)
                    await DeleteQuietlyAsync(photoKey);

                throw;
            }

            return Result.Success();
        },
        conflictMessage: "Görev bu sırada değişti. Lütfen tekrar deneyin.",
        cancellationToken);
    }
}