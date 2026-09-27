using Scootly.Domain.Fleet;
using Scootly.Domain.Geo;
using Scootly.Domain.Riding;
using Scootly.Domain.Telemetry;

namespace Scootly.Application.Abstractions;

/// <summary>
/// Okuma tarafı: liste, filtre ve projeksiyon sorguları doğrudan bu arayüz üzerinden yazılır (bkz. ADR 0002).
/// Yazma işlemleri repository'ler ve <see cref="IUnitOfWork"/> üzerinden yapılır.
/// </summary>
public interface IApplicationDbContext
{
    IQueryable<Vehicle> Vehicles { get; }
    IQueryable<Ride> Rides { get; }
    IQueryable<TelemetryReading> TelemetryReadings { get; }
    IQueryable<ServiceArea> ServiceAreas { get; }
}
