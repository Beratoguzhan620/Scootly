using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scootly.Domain.Riding;

namespace Scootly.Infrastructure.Persistence.Configurations;

public sealed class RideConfiguration : IEntityTypeConfiguration<Ride>
{
    public void Configure(EntityTypeBuilder<Ride> builder)
    {
        builder.ToTable("Rides");

        builder.HasKey(r => r.Id);

        builder.Property(r => r.DriverId).IsRequired();
        builder.Property(r => r.VehicleId).IsRequired();
        builder.Property(r => r.StartedAt).IsRequired();

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        // 69. gun. NULL = odeme henuz sonuclanmadi; mevcut satirlar icin
        // migration'da da NULL kaliyor (bkz. RidePaymentStatus).
        builder.Property(r => r.PaymentStatus)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.OwnsOne(r => r.StartLocation, location =>
        {
            location.Property(l => l.Latitude).HasColumnName("StartLatitude");
            location.Property(l => l.Longitude).HasColumnName("StartLongitude");
        });

        builder.OwnsOne(r => r.EndLocation, location =>
        {
            location.Property(l => l.Latitude).HasColumnName("EndLatitude");
            location.Property(l => l.Longitude).HasColumnName("EndLongitude");
        });

        builder.Navigation(r => r.StartLocation).IsRequired();
    }
}