using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scootly.Domain.Fleet;
using Scootly.Domain.Riding;

namespace Scootly.Infrastructure.Persistence.Configurations;

public sealed class RideConfiguration : IEntityTypeConfiguration<Ride>
{
    public void Configure(EntityTypeBuilder<Ride> builder)
    {
        builder.ToTable("Rides");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.DriverId)
            .IsRequired();

        builder.Property(r => r.VehicleId)
            .HasConversion(
                vehicleId => vehicleId.Value,
                value => new VehicleId(value))
            .HasColumnName("VehicleId")
            .IsRequired();

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.Property(r => r.StartedAt)
            .IsRequired();

        builder.Property(r => r.EndedAt);

        builder.Property(r => r.Fare)
            .HasPrecision(10, 2);

        // StartLocation / EndLocation (GeoPoint) eşlemesi Gün 12'de eklenecek

        builder.Ignore(r => r.DomainEvents);
    }
}