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

        builder.Property(t => t.VehicleId).IsRequired();

        builder.Property(t => t.Type)
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(t => t.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(t => t.Reason)
            .HasMaxLength(FieldTask.MaxReasonLength)
            .IsRequired();

        builder.Property(t => t.CreatedAt).IsRequired();
        builder.Property(t => t.CompletedAt);

        // Ayni arac icin ayni turde yalnizca BIR acik gorev. Kural uygulama
        // kodunda da kontrol ediliyor, ama iki tuketici ayni anda "acik gorev
        // yok" gorebilir; iki ayri surecteki iki nesne birbirini goremez.
        // Yarisin kazanani burada belirleniyor: ikinci INSERT hata verir, mesaj
        // yeniden denenir ve ikinci denemede "zaten var" yolundan cikar.
        // Kismi indeks: tamamlanmis gorevler kurala dahil degil, ayni araca
        // ertesi hafta yeni bir gorev acilabilmeli.
        builder.HasIndex(t => new { t.VehicleId, t.Type })
            .IsUnique()
            .HasFilter("\"Status\" = 'Open'")
            .HasDatabaseName("UX_FieldTasks_AcikGorev");
    }
}
