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

        // Kimlik olayin kendi EventId'si; veritabani uretmiyor.
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.EventType).HasMaxLength(100).IsRequired();

        // jsonb: gerektiginde "hangi surusun olayi bekliyor" diye SQL ile
        // icine bakilabilsin.
        builder.Property(m => m.Payload).HasColumnType("jsonb").IsRequired();

        builder.Property(m => m.CreatedAt).IsRequired();
        builder.Property(m => m.ProcessedAt);
        builder.Property(m => m.Attempts).IsRequired();
        builder.Property(m => m.LastError).HasMaxLength(OutboxMessage.MaxErrorLength);

        // Gondericinin tek sorgusu: bekleyenler, eskiden yeniye. Kismi indeks
        // yalnizca bekleyen satirlari tutuyor; gonderilmis milyonlarca satir
        // indekse girmiyor.
        builder.HasIndex(m => m.CreatedAt)
            .HasFilter("\"ProcessedAt\" IS NULL")
            .HasDatabaseName("IX_OutboxMessages_Bekleyen");
    }
}
