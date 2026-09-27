using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scootly.Infrastructure.Messaging.Idempotency;

namespace Scootly.Infrastructure.Persistence.Configurations;

public sealed class ProcessedMessageConfiguration : IEntityTypeConfiguration<ProcessedMessage>
{
    public void Configure(EntityTypeBuilder<ProcessedMessage> builder)
    {
        builder.ToTable("ProcessedMessages");

        builder.HasKey(m => new { m.MessageId, m.Consumer });

        builder.Property(m => m.Consumer).HasMaxLength(ProcessedMessage.ConsumerMaxLength);
        builder.Property(m => m.ProcessedAt).IsRequired();

        builder.HasIndex(m => m.ProcessedAt)
            .HasDatabaseName("IX_ProcessedMessages_ProcessedAt");
    }
}
