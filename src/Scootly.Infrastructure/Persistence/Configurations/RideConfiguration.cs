using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scootly.Application.Common;
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

        builder.Property(r => r.Fare).HasPrecision(10, 2);

        builder.Property(r => r.PaymentStatus)
            .HasConversion<string>()
            .HasMaxLength(20)
            .HasDefaultValue(PaymentStatus.None)
            .HasSentinel(PaymentStatus.None);

        builder.Property(r => r.LastPaymentError).HasMaxLength(Ride.PaymentErrorMaxLength);

        // Ödeme durumu webhook ve tüketici tarafından eşzamanlı güncellenebilir; son yazan kazanmamalı.
        builder.Property<uint>("xmin").IsRowVersion();

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

        // Sürücü başına ve araç başına en fazla bir aktif sürüş.
        builder.HasIndex(r => r.DriverId)
            .HasDatabaseName(ConstraintNames.OneActiveRidePerDriver)
            .IsUnique()
            .HasFilter("\"Status\" = 'Active'");

        builder.HasIndex(r => r.VehicleId)
            .HasDatabaseName(ConstraintNames.OneActiveRidePerVehicle)
            .IsUnique()
            .HasFilter("\"Status\" = 'Active'");

        builder.HasIndex(r => new { r.Status, r.StartedAt })
            .HasDatabaseName("IX_Rides_Status_StartedAt");

        builder.HasIndex(r => r.PaymentStatus)
            .HasDatabaseName("IX_Rides_PaymentStatus");

        builder.HasIndex(r => r.EndedAt)
            .HasDatabaseName("IX_Rides_EndedAt");

        builder.Ignore(r => r.Duration);
        builder.Ignore(r => r.NextPaymentIdempotencyKey);
        builder.Ignore(r => r.DomainEvents);
    }
}
