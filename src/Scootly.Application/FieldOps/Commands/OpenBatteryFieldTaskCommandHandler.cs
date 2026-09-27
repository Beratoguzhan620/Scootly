using Scootly.Application.Abstractions;
using Scootly.Domain.Common;
using Scootly.Domain.FieldOps;

namespace Scootly.Application.FieldOps.Commands;

/// <summary>
/// Bataryası düşen araç için saha görevi açar (64. gün).
/// </summary>
/// <remarks>
/// <para>
/// <b>Aynı araç için ikinci açık görev açılmıyor.</b> Batarya tarayıcısı her
/// turda (beş dakikada bir) eşiğin altındaki bütün araçlar için olay
/// yayınlıyor; saha ekibine aynı araç için her beş dakikada bir yeni görev
/// düşmemeli. Açık görev varsa istek başarıyla ve hiçbir şey yapmadan biter.
/// </para>
/// <para>
/// <b>İki tüketici aynı anda çalışırsa</b> ikisi de "açık görev yok" görüp
/// ikisi de ekleyebilir. O durumu veritabanındaki kısmi tekil indeks
/// yakalıyor: ikinci kayıt istisna fırlatır, mesaj geçici hata olarak birkaç
/// saniye sonra yeniden denenir ve ikinci denemede bu kontrol "zaten var" der.
/// Bu yüzden burada EF Core'un <c>DbUpdateException</c>'ı yakalanmıyor —
/// yeniden deneme mekanizması zaten doğru sonuca götürüyor ve Application'ın
/// bir EF tipine daha bağlanması gerekmiyor.
/// </para>
/// </remarks>
public sealed class OpenBatteryFieldTaskCommandHandler
{
    private readonly IFieldTaskRepository _fieldTaskRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IClock _clock;

    public OpenBatteryFieldTaskCommandHandler(
        IFieldTaskRepository fieldTaskRepository,
        IUnitOfWork unitOfWork,
        IClock clock)
    {
        _fieldTaskRepository = fieldTaskRepository;
        _unitOfWork = unitOfWork;
        _clock = clock;
    }

    public async Task<Result> Handle(OpenBatteryFieldTaskCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var zatenVar = await _fieldTaskRepository.HasOpenTaskAsync(
            command.VehicleId, FieldTaskType.BatteryReplacement, cancellationToken);

        if (zatenVar)
            return Result.Success();

        FieldTask gorev;

        try
        {
            gorev = FieldTask.ForLowBattery(command.VehicleId, command.BatteryPercentage, _clock.UtcNow);
        }
        catch (DomainException ex)
        {
            return Result.Failure(ex.Message);
        }

        await _fieldTaskRepository.AddAsync(gorev, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return Result.Success();
    }
}
