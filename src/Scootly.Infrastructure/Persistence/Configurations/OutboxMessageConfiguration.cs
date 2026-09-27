using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scootly.Infrastructure.Messaging.Outbox;

namespace Scootly.Infrastructure.Persistence.Configurations;

public sealed class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
{
    public void Configure(EntityTypeBuilder<OutboxMessage> builder)
    {
        builder.ToTable("OutboxMessages");

        builder.HasKey(m => m.Id);

        builder.Property(m => m.EventType).IsRequired().HasMaxLength(200);
        builder.Property(m => m.Payload).IsRequired();
        builder.Property(m => m.CreatedAt).IsRequired();
        builder.Property(m => m.Attempts).HasDefaultValue(0);
        builder.Property(m => m.LastError).HasMaxLength(OutboxMessage.LastErrorMaxLength);

        builder.HasIndex(m => m.ProcessedAt)
            .HasDatabaseName("IX_OutboxMessages_ProcessedAt");

        // Yayınlanmayı bekleyenlerin oluşturulma sırasıyla hızlı taranması için kısmi indeks.
        builder.HasIndex(m => m.CreatedAt)
            .HasDatabaseName("IX_OutboxMessages_Pending_CreatedAt")
            .HasFilter("\"ProcessedAt\" IS NULL");
    }
}
