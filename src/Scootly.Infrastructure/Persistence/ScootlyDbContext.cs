using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Scootly.Application.Abstractions;
using Scootly.Domain.Billing;
using Scootly.Domain.FieldOps;
using Scootly.Domain.Fleet;
using Scootly.Domain.Riding;
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

    /// <summary>Saha görevleri (64. gün).</summary>
    public DbSet<FieldTask> FieldTasks => Set<FieldTask>();

    /// <summary>Ödemesi alınamamış sürüşlerin borçları (69. gün).</summary>
    public DbSet<OutstandingDebt> OutstandingDebts => Set<OutstandingDebt>();

    /// <summary>Kuyruğa gönderilmeyi bekleyen olaylar (66. gün).</summary>
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();

    /// <summary>Tüketicilerin işlediği mesajlar (68. gün).</summary>
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

    IQueryable<Vehicle> IApplicationDbContext.Vehicles => Vehicles;
    IQueryable<Ride> IApplicationDbContext.Rides => Rides;

    public void AddVehicle(Vehicle vehicle)
    {
        Vehicles.Add(vehicle);
    }

    public void AddRide(Ride ride)
    {
        Rides.Add(ride);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ScootlyDbContext).Assembly);
    }
}