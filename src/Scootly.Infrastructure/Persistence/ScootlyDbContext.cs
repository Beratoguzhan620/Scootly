using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Scootly.Application.Abstractions;
using Scootly.Domain.Fleet;
using Scootly.Domain.Riding;
using Scootly.Domain.Telemetry;
using Scootly.Infrastructure.Identity;
using Scootly.Infrastructure.Messaging.Idempotency;
using Scootly.Infrastructure.Messaging.Outbox;

namespace Scootly.Infrastructure.Persistence;

public sealed class ScootlyDbContext
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>, IApplicationDbContext, IUnitOfWork
{
    public ScootlyDbContext(DbContextOptions<ScootlyDbContext> options) : base(options) { }

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Ride> Rides => Set<Ride>();
    public DbSet<TelemetryReading> TelemetryReadings => Set<TelemetryReading>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

    IQueryable<Vehicle> IApplicationDbContext.Vehicles => Vehicles;
    IQueryable<Ride> IApplicationDbContext.Rides => Rides;
    IQueryable<TelemetryReading> IApplicationDbContext.TelemetryReadings => TelemetryReadings;

    public void AddVehicle(Vehicle vehicle)
    {
        Vehicles.Add(vehicle);
    }

    public void AddRide(Ride ride)
    {
        Rides.Add(ride);
    }

    public void AddTelemetryReading(TelemetryReading reading)
    {
        TelemetryReadings.Add(reading);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ScootlyDbContext).Assembly);
    }
}