using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scootly.Domain.Fleet;

namespace Scootly.Infrastructure.Persistence.Configurations;

public sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    /// <summary>Sürüm damgası sütununun adı (38. gün).</summary>
    public const string SurumSutunu = "Version";

    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("Vehicles");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        // --- 38. gün: iyimser eşzamanlılık ---
        //
        // Sürüm damgası GÖLGE (shadow) özellik olarak tanımlı: alan modelinde
        // karşılığı olan bir alan yok. Gerekçe, Vehicle'ın bir iş kavramı
        // olması — "bu satır kaç kez güncellendi" ise bir kalıcılık detayı.
        // Alan modeline bir Version alanı eklemek, aracın iş kurallarıyla
        // hiç ilgisi olmayan bir sayıyı domain'e sokmak olurdu.
        //
        // Değerini ScootlyDbContext.SaveChangesAsync artırıyor.
        //
        // NEDEN IsRowVersion() DEĞİL: IsRowVersion(), SQL Server'ın rowversion
        // tipine karşılık gelir ve PostgreSQL'de karşılığı yoktur. Npgsql ile
        // kullanıldığında ya hiç çalışmaz ya da sessizce korumasız bırakır —
        // yani test yeşil görünürken iki kişi aynı aracı kiralayabilir.
        // PostgreSQL'e özgü alternatif, sistem sütunu xmin'i damga olarak
        // kullanmaktır; onu seçmedik çünkü sağlayıcıya özgü ve migration
        // üretimiyle sürtüşüyor (bkz. ADR 0012).
        builder.Property<int>(SurumSutunu)
               .IsConcurrencyToken()
               .HasDefaultValue(0);

        builder.OwnsOne(v => v.Model, model =>
        {
            // 32. gün: 100 karakterden 64'e indirildi.
            model.Property(m => m.Brand).HasColumnName("Brand").HasMaxLength(64);
            model.Property(m => m.RangeKm).HasColumnName("RangeKm");
        });

        builder.OwnsOne(v => v.Battery, battery =>
        {
            battery.Property(b => b.Percentage).HasColumnName("BatteryPercentage");
        });

        builder.OwnsOne(v => v.Location, location =>
        {
            location.Property(l => l.Latitude).HasColumnName("Latitude");
            location.Property(l => l.Longitude).HasColumnName("Longitude");

            // İndeks BU BLOĞUN İÇİNDE tanımlanmak zorunda: Latitude ve Longitude
            // Vehicle'ın değil, sahip olunan GeoPoint tipinin özellikleri. Aynı
            // tabloya yazılıyor olmaları onları Vehicle'ın özelliği yapmıyor.
            location.HasIndex(l => new { l.Latitude, l.Longitude })
                    .HasDatabaseName("ix_vehicles_konum");
        });

        builder.Navigation(v => v.Model).IsRequired();
        builder.Navigation(v => v.Battery).IsRequired();
        builder.Navigation(v => v.Location).IsRequired();

        // Kısmi (partial) indeks: Status yalnızca dört değer alıyor, seçiciliği
        // düşük. Tam indeks planlayıcı tarafından görmezden gelinirdi; koşullu
        // indeks yalnızca ilgilenilen satırları tutuyor.
        builder.HasIndex(v => v.Status)
               .HasDatabaseName("ix_vehicles_durum")
               .HasFilter("\"Status\" = 'Available'");
    }
}
