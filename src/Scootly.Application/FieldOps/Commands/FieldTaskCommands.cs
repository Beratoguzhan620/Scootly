using Scootly.Application.Abstractions;
using Scootly.Application.Abstractions.Exceptions;
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

    /// <returns>
    /// Oluşturulan görevin kimliği; aynı araç+tür için zaten açık bir görev varsa <c>null</c>
    /// (yeni görev açılmaz, işlem yine de başarılıdır — tekrar teslim edilen olaylar için idempotent).
    /// </returns>
    public async Task<Result<Guid?>> Handle(CreateFieldTaskCommand command, CancellationToken cancellationToken = default)
    {
        if (await _fieldTaskRepository.HasOpenTaskAsync(command.VehicleId, command.Type, cancellationToken))
            return Result<Guid?>.Success(null);

        var fieldTask = new FieldTask(Guid.NewGuid(), command.VehicleId, command.Type, _clock.UtcNow);

        try
        {
            await _fieldTaskRepository.AddAsync(fieldTask, cancellationToken);
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (UniqueConstraintViolationException ex) when (ex.ConstraintName == ConstraintNames.OneOpenFieldTaskPerVehicleAndType)
        {
            // Kontrol ile kayıt arasında başka bir süreç aynı görevi açtı; sonuç aynı.
            _unitOfWork.DiscardChanges();
            return Result<Guid?>.Success(null);
        }

        return Result<Guid?>.Success(fieldTask.Id);
    }

    public Task<Result> Handle(AssignFieldTaskCommand command, CancellationToken cancellationToken = default)
    {
        return OptimisticConcurrency.ExecuteAsync(_unitOfWork, async token =>
        {
            var fieldTask = await _fieldTaskRepository.GetByIdAsync(command.FieldTaskId, token);

            if (fieldTask is null)
                return Result.NotFound("Görev bulunamadı.");

            // Durum ihlalleri (örn. görev zaten üstlenilmiş) DomainException olarak çakışmaya (409) çevrilir.
            fieldTask.Assign(command.OperatorId, _clock.UtcNow);
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
                return Task.FromResult(Result.Validation("Fotoğraf depolama kapalı."));

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

            if (fieldTask.AssignedTo != command.OperatorId)
                return Result.Forbidden("Bu görev size atanmamış.");

            // Nesne adı sunucuda üretilir; kullanıcının dosya adı hiçbir yerde kullanılmaz.
            string? photoKey = photoInfo is null
                ? null
                : $"field-tasks/{fieldTask.Id:N}/{Guid.NewGuid():N}{photoInfo.Extension}";

            fieldTask.Complete(command.OperatorId, _clock.UtcNow, command.Note, photoKey);

            if (photoKey is not null)
            {
                try
                {
                    await _fileStorage.PutAsync(photoKey, command.PhotoContent!, photoInfo!.ContentType, token);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _unitOfWork.DiscardChanges();
                    return Result.Failure("Fotoğraf yüklenemedi. Lütfen tekrar deneyin.");
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

    private async Task DeleteQuietlyAsync(string objectKey)
    {
        try
        {
            await _fileStorage.DeleteAsync(objectKey, CancellationToken.None);
        }
        catch (Exception)
        {
            // En iyi çaba temizlik: yükleme başarılı ama kayıt başarısız olursa yetim nesne kalabilir (teknik borç).
        }
    }
}
