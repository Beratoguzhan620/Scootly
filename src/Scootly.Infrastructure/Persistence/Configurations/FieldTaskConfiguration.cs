using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scootly.Domain.FieldOps;

namespace Scootly.Infrastructure.Persistence.Configurations;

public sealed class FieldTaskConfiguration : IEntityTypeConfiguration<FieldTask>
{
    public void Configure(EntityTypeBuilder<FieldTask> builder)
    {
        builder.ToTable("FieldTasks");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Type)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(t => t.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(t => t.Note)
            .HasMaxLength(500);

        builder.Property(t => t.CreatedAt);

        builder.HasIndex(t => t.VehicleId)
            .HasDatabaseName("IX_FieldTasks_VehicleId");

        // Dokümanın istediği kural: araç + tür başına aynı anda yalnızca bir açık (Open ya da Assigned) görev olabilir.
        builder.HasIndex(t => new { t.VehicleId, t.Type })
            .HasDatabaseName("IX_FieldTasks_OneOpenTaskPerVehicleAndType")
            .IsUnique()
            .HasFilter("\"Status\" <> 'Completed'");

        builder.Property<uint>("xmin").IsRowVersion();

        builder.Ignore(t => t.DomainEvents);
    }
}