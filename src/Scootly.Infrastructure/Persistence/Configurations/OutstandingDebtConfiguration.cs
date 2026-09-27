using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scootly.Domain.Billing;

namespace Scootly.Infrastructure.Persistence.Configurations;

public sealed class OutstandingDebtConfiguration : IEntityTypeConfiguration<OutstandingDebt>
{
    public void Configure(EntityTypeBuilder<OutstandingDebt> builder)
    {
        builder.ToTable("OutstandingDebts");

        builder.HasKey(d => d.Id);

        builder.Property(d => d.DriverId).IsRequired();
        builder.Property(d => d.RideId).IsRequired();
        builder.Property(d => d.Amount).HasPrecision(10, 2).IsRequired();
        builder.Property(d => d.Reason).HasMaxLength(OutstandingDebt.MaxReasonLength).IsRequired();
        builder.Property(d => d.CreatedAt).IsRequired();
        builder.Property(d => d.SettledAt);

        // Bir surus icin en fazla bir borc. Tekrar gelen "odeme basarisiz"
        // sonucu ikinci bir borc acamaz.
        builder.HasIndex(d => d.RideId).IsUnique().HasDatabaseName("UX_OutstandingDebts_RideId");
        builder.HasIndex(d => d.DriverId).HasDatabaseName("IX_OutstandingDebts_DriverId");
    }
}
