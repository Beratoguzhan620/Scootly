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
            .HasMaxLength(20);

        builder.OwnsOne(v => v.Model, model =>
        {
            model.Property(m => m.Brand).HasColumnName("Brand").HasMaxLength(100);
            model.Property(m => m.RangeKm).HasColumnName("RangeKm");
        });

        builder.OwnsOne(v => v.Battery, battery =>
        {
            battery.Property(b => b.Percentage).HasColumnName("BatteryPercentage");
        });

        builder.OwnsOne(v => v.Location, location =>
        {
            location.Property(l => l.Latitude).HasColumnName("Latitude");
            location.Property(l => l.Longitude).HasColumnName("Longitude");
        });

        builder.Navigation(v => v.Model).IsRequired();
        builder.Navigation(v => v.Battery).IsRequired();
        builder.Navigation(v => v.Location).IsRequired();
    }
}