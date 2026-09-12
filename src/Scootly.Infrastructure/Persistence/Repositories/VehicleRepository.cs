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

    public async Task<Vehicle?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await _dbContext.Vehicles.FirstOrDefaultAsync(v => v.Id == id, cancellationToken);
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
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddAsync(Vehicle vehicle, CancellationToken cancellationToken = default)
    {
        await _dbContext.Vehicles.AddAsync(vehicle, cancellationToken);
    }
}
