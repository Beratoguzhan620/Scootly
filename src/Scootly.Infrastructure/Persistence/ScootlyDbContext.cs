using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Scootly.Application.Abstractions;
using Scootly.Application.Abstractions.Exceptions;
using Scootly.Domain.Common;
using Scootly.Domain.Fleet;
using Scootly.Domain.Geo;
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
    public DbSet<ServiceArea> ServiceAreas => Set<ServiceArea>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
    public DbSet<ProcessedMessage> ProcessedMessages => Set<ProcessedMessage>();

    IQueryable<Vehicle> IApplicationDbContext.Vehicles => Vehicles;
    IQueryable<Ride> IApplicationDbContext.Rides => Rides;
    IQueryable<TelemetryReading> IApplicationDbContext.TelemetryReadings => TelemetryReadings;
    IQueryable<ServiceArea> IApplicationDbContext.ServiceAreas => ServiceAreas;

    /// <summary>
    /// Kaydetmeden önce aggregate'lerin domain olaylarını aynı transaction içinde outbox'a yazar
    /// (olay kaybı ya da "olay yayınlandı ama veri kaydedilmedi" durumu oluşmaz) ve
    /// EF/Npgsql istisnalarını Application katmanının anladığı istisnalara çevirir.
    /// </summary>
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        WriteDomainEventsToOutbox();

        try
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException("Kayıt, okunduktan sonra başka bir işlem tarafından değiştirildi.", ex);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } postgresException)
        {
            throw new UniqueConstraintViolationException(postgresException.ConstraintName, ex);
        }
    }

    public void DiscardChanges() => ChangeTracker.Clear();

    private void WriteDomainEventsToOutbox()
    {
        var aggregates = ChangeTracker.Entries<AggregateRoot>()
            .Select(entry => entry.Entity)
            .Where(aggregate => aggregate.DomainEvents.Count > 0)
            .ToList();

        foreach (var aggregate in aggregates)
        {
            foreach (var domainEvent in aggregate.DomainEvents)
            {
                var message = DomainEventOutboxMapper.ToOutboxMessage(domainEvent);

                if (message is not null)
                    OutboxMessages.Add(message);
            }

            // Olaylar outbox'a aktarıldı; aynı birimin yeniden kaydedilmesi olayları çoğaltmamalı.
            aggregate.ClearDomainEvents();
        }
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ScootlyDbContext).Assembly);
    }
}
