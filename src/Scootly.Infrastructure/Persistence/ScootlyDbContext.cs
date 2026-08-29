using Microsoft.EntityFrameworkCore;
using Scootly.Application.Abstractions;
using Scootly.Domain.Fleet;
using Scootly.Domain.Riding;

namespace Scootly.Infrastructure.Persistence;

public sealed class ScootlyDbContext : DbContext, IApplicationDbContext, IUnitOfWork
{
    public ScootlyDbContext(DbContextOptions<ScootlyDbContext> options)
        : base(options)
    {
    }

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Ride> Rides => Set<Ride>();

    IQueryable<Vehicle> IApplicationDbContext.Vehicles => Vehicles;
    IQueryable<Ride> IApplicationDbContext.Rides => Rides;

    public void AddVehicle(Vehicle vehicle) => Vehicles.Add(vehicle);

    public void AddRide(Ride ride) => Rides.Add(ride);

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ScootlyDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }
}