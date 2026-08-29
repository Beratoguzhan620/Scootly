using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scootly.Domain.Fleet;

namespace Scootly.Infrastructure.Persistence.Configurations;

public sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("Vehicles");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.Status)
            .HasConversion<string>()
            .HasMaxLength(20)
            .IsRequired();

        builder.OwnsOne(v => v.Model, model =>
        {
            model.Property(m => m.Brand)
                .HasColumnName("Brand")
                .HasMaxLength(100)
                .IsRequired();

            model.Property(m => m.RangeKm)
                .HasColumnName("RangeKm")
                .IsRequired();
        });

        builder.OwnsOne(v => v.Battery, battery =>
        {
            battery.Property(b => b.Percentage)
                .HasColumnName("BatteryPercentage")
                .IsRequired();
        });

        // Location (GeoPoint) eşlemesi Gün 12'de eklenecek

        builder.Ignore(v => v.DomainEvents);
    }
}
