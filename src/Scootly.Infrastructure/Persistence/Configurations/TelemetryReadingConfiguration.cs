using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scootly.Domain.Telemetry;

namespace Scootly.Infrastructure.Persistence.Configurations;

public sealed class TelemetryReadingConfiguration : IEntityTypeConfiguration<TelemetryReading>
{
    public void Configure(EntityTypeBuilder<TelemetryReading> builder)
    {
        builder.ToTable("TelemetryReadings");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.VehicleId).IsRequired();
        builder.Property(t => t.BatteryPercentage).IsRequired();
        builder.Property(t => t.RecordedAt).IsRequired();

        builder.HasIndex(t => t.VehicleId)
            .HasDatabaseName("IX_TelemetryReadings_VehicleId");

        builder.HasIndex(t => t.RecordedAt)
            .HasDatabaseName("IX_TelemetryReadings_RecordedAt");

        builder.OwnsOne(t => t.Location, location =>
        {
            location.Property(l => l.Latitude).HasColumnName("Latitude");
            location.Property(l => l.Longitude).HasColumnName("Longitude");
        });

        builder.Navigation(t => t.Location).IsRequired();
    }
}