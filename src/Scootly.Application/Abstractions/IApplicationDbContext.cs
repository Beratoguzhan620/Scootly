using Scootly.Domain.Fleet;
using Scootly.Domain.Pricing;
using Scootly.Domain.Riding;
using Scootly.Domain.Telemetry;

namespace Scootly.Application.Abstractions;

public interface IApplicationDbContext
{
    IQueryable<Vehicle> Vehicles { get; }
    IQueryable<Ride> Rides { get; }

    /// <summary>Tarifeler (46. gün).</summary>
    IQueryable<Tariff> Tariffs { get; }

    /// <summary>
    /// Telemetri kayıtları — yalnızca OKUMA için (51. gün).
    /// </summary>
    /// <remarks>
    /// Yazma yolu bilinçli olarak burada değil: telemetri
    /// <see cref="ITelemetryWriter"/> üzerinden toplu yazılıyor. İki yolu da
    /// açık bırakmak, bir gün birinin döngü içinde <c>AddAsync</c> çağırıp
    /// saniyede yüzlerce tekil INSERT üretmesiyle biterdi.
    /// </remarks>
    IQueryable<TelemetryReading> TelemetryReadings { get; }

    void AddRide(Ride ride);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}