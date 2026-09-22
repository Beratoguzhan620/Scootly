using Microsoft.EntityFrameworkCore;
using Scootly.Application.Abstractions;
using Scootly.Domain.Fleet;

namespace Scootly.Infrastructure.Persistence.Repositories;

public sealed class VehicleRepository : IVehicleRepository
{
    private readonly ScootlyDbContext _dbContext;

    public VehicleRepository(ScootlyDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    /// <summary>Aracı DEĞİŞTİRİLMEK ÜZERE okur.</summary>
    /// <remarks>
    /// <c>AsTracking()</c> burada zorunlu (41. gün): bağlamın varsayılanı artık
    /// takipsiz okuma. Bu satır olmasaydı <c>vehicle.Reserve()</c> çağrısı
    /// nesneyi değiştirir, <c>SaveChangesAsync</c> hiçbir değişiklik görmez ve
    /// <b>sessizce 0 satır</b> yazardı — istek 200 dönerdi, araç rezerve
    /// edilmezdi. Repository'nin işi değiştirilecek varlığı getirmek olduğuna
    /// göre takip burada isteniyor; sorgu handler'larında istenmiyor.
    /// </remarks>
    public async Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Vehicles
            .AsTracking()
            .FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
    }

    /// <summary>
    /// Aracı okurken satırı KİLİTLER (39. gün — kötümser kilitleme).
    /// </summary>
    /// <remarks>
    /// <para>
    /// <c>FOR UPDATE</c>, satırı okuyan işlem bitene kadar başka bir işlemin o
    /// satırı değiştirmesini engeller. İyimser yaklaşımdan farkı: iyimser olan
    /// çakışmayı <b>yazma anında tespit eder</b>, kötümser olan <b>baştan
    /// engeller</b>.
    /// </para>
    /// <para>
    /// Bu metot <see cref="IVehicleRepository"/> arayüzüne BİLEREK eklenmedi.
    /// Kilitleme bir kalıcılık stratejisidir; arayüze konsaydı her uygulayan
    /// (ve her test sahtesi) satır kilidi kavramını bilmek zorunda kalırdı.
    /// Bugün yalnızca 39. günün deadlock deneyi kullanıyor.
    /// </para>
    /// <para>
    /// <b>Kilit sırası disiplini:</b> birden fazla tablo kilitlenecekse her yerde
    /// AYNI sırayla kilitlenmeli. Sıra tutarsız olduğunda iki işlem birbirinin
    /// kilidini bekler ve veritabanı taraflardan birini iptal etmek zorunda
    /// kalır. Bu projede sıra: önce Vehicles, sonra Rides.
    /// </para>
    /// </remarks>
    public async Task<Vehicle?> GetByIdForUpdateAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Vehicles
            .FromSql($"SELECT * FROM \"Vehicles\" WHERE \"Id\" = {id} FOR UPDATE")
            // 41. günden sonra gerekli: adı "ForUpdate" olan bir metodun
            // döndürdüğü nesne takip edilmiyorsa, kilidi alıp hiçbir şey
            // yazmamış olurduk.
            .AsTracking()
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken = default)
    {
        await _dbContext.Vehicles.AddAsync(vehicle, cancellationToken);
    }
}
