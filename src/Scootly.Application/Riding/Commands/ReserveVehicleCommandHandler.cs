using Scootly.Application.Abstractions;
using Scootly.Application.Common;
using Scootly.Domain.Common;

namespace Scootly.Application.Riding.Commands;

public sealed class ReserveVehicleCommandHandler
{
    /// <summary>
    /// Çakışma durumunda kullanıcıya dönen mesaj.
    /// </summary>
    /// <remarks>
    /// Otomatik yeniden deneme YAPILMIYOR. Araç kiralama gibi bir işlemde
    /// sessizce tekrar denemek, kullanıcının artık istemediği ya da yanındaki
    /// başka bir aracı kiralamasına yol açabilir. Kullanıcıya ne olduğunu
    /// söyleyip kararı ona bırakmak daha doğru.
    /// </remarks>
    public const string CakismaMesaji = "Biri sizden önce davrandı; araç artık müsait değil.";

    private readonly IVehicleRepository _vehicleRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly INearbyVehicleCache _nearbyCache;

    public ReserveVehicleCommandHandler(
        IVehicleRepository vehicleRepository,
        IUnitOfWork unitOfWork,
        INearbyVehicleCache nearbyCache)
    {
        _vehicleRepository = vehicleRepository;
        _unitOfWork = unitOfWork;
        _nearbyCache = nearbyCache;
    }

    public async Task<Result> Handle(ReserveVehicleCommand command, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);

        var vehicle = await _vehicleRepository.GetByIdAsync(command.VehicleId, cancellationToken);

        if (vehicle is null)
        {
            return Result.Failure("Araç bulunamadı.");
        }

        vehicle.Reserve();

        try
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (ConcurrencyConflictException)
        {
            // 38. gün. Buraya düşmenin tek yolu: araç okunduktan sonra, biz
            // yazmadan önce başka bir işlemin aynı satırı değiştirmesi.
            // Sürüm damgası olmasaydı bu durum hiç fark edilmez, iki sürücü de
            // "rezerve ettiniz" yanıtı alırdı.
            return Result.Failure(CakismaMesaji);
        }

        // 49. gün — geçersizleştirme. Araç artık müsait değil; harita
        // önbelleğinde onu müsait gösteren her girdi yanlış.
        //
        // SIRA ÖNEMLİ: silme, kaydetmeden SONRA. Önce silinseydi, kaydetme ile
        // silme arasındaki aralıkta gelen bir okuma eski veriyi yeniden
        // önbelleğe yazar ve silme boşa giderdi.
        //
        // Silme başarısız olursa istek YİNE DE başarılı sayılıyor: rezervasyon
        // gerçekten yapıldı. Bunun bedeli en fazla beş saniyelik bayat harita
        // (bkz. NearbyVehicleCache.YasamSuresi) — kullanıcıya "rezervasyon
        // başarısız" demekten çok daha az zararlı.
        await _nearbyCache.InvalidateAsync(cancellationToken);

        return Result.Success();
    }
}
