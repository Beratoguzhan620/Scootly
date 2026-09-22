using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scootly.Domain.Pricing;

namespace Scootly.Infrastructure.Persistence.Configurations;

public sealed class TariffConfiguration : IEntityTypeConfiguration<Tariff>
{
    public void Configure(EntityTypeBuilder<Tariff> builder)
    {
        builder.ToTable("Tariffs");

        builder.HasKey(t => t.Id);

        builder.Property(t => t.Name)
               .HasMaxLength(Tariff.MaxNameLength)
               .IsRequired();

        builder.Property(t => t.IsActive)
               .IsRequired();

        builder.OwnsOne(t => t.UnlockFee, money =>
        {
            money.Property(m => m.Amount).HasColumnName("UnlockAmount").HasColumnType("numeric(10,2)");
            money.Property(m => m.Currency).HasColumnName("UnlockCurrency").HasMaxLength(3);
        });

        builder.OwnsOne(t => t.PerMinuteFee, money =>
        {
            money.Property(m => m.Amount).HasColumnName("PerMinuteAmount").HasColumnType("numeric(10,2)");
            money.Property(m => m.Currency).HasColumnName("PerMinuteCurrency").HasMaxLength(3);
        });

        builder.Navigation(t => t.UnlockFee).IsRequired();
        builder.Navigation(t => t.PerMinuteFee).IsRequired();

        // "Aynı anda yalnızca bir aktif tarife" kuralı BURADA zorlanıyor,
        // yalnızca kodda değil. Kısmi tekil indeks: yalnızca IsActive = true
        // olan satırları kapsıyor, pasif tarifelerin sayısı sınırsız.
        //
        // Kuralı yalnızca uygulama katmanında tutmak yetmezdi: iki eşzamanlı
        // istek "aktif tarife var mı" diye sorup ikisi de "yok" cevabını alır
        // ve ikisi de yazardı — 36. günde ölçtüğümüz yarış durumunun aynısı.
        // Veritabanı kısıtı bu yarışı kazananı tek bırakarak çözüyor.
        builder.HasIndex(t => t.IsActive)
               .HasDatabaseName("ux_tarife_aktif")
               .IsUnique()
               .HasFilter("\"IsActive\" = true");
    }
}
