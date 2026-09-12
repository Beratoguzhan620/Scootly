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

        builder.Property(r => r.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        // 32. gün: ücret için tip gözden geçirmesi.
        // decimal, kayan noktalı (double) değil — para birimi hesaplarında
        // double kullanmak 0.1 + 0.2 = 0.30000000000000004 sınıfı hatalara yol
        // açar ve bu hatalar fatura toplamlarında birikir. numeric(10,2):
        // 99.999.999,99'a kadar, kuruş hassasiyetinde.
        builder.Property(r => r.Fare)
            .HasColumnType("numeric(10,2)");

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

        // --- 33. gün: indeksler ---

        // "Bu sürücünün sürüşleri" — 24. gündeki sahiplik kontrolü ve ileride
        // sürücünün geçmiş ekranı bu sütundan geçiyor.
        builder.HasIndex(r => r.DriverId)
               .HasDatabaseName("ix_rides_surucu");

        // Araç bazlı sorgular ve JOIN'ler.
        builder.HasIndex(r => r.VehicleId)
               .HasDatabaseName("ix_rides_arac");

        // "Son 24 saatte tamamlanan sürüşler" — bileşik indeks.
        // Status önce geliyor çünkü sorgu her zaman onunla eşitlik kontrolü
        // yapıyor; EndedAt sonra çünkü aralık (range) koşulu bileşik indekste
        // en sonda olmalı. Sıra ters olsaydı indeks aralıktan sonrasını
        // kullanamazdı.
        builder.HasIndex(r => new { r.Status, r.EndedAt })
               .HasDatabaseName("ix_rides_durum_bitis");
    }
}
