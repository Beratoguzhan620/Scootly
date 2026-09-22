using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scootly.Domain.Telemetry;

namespace Scootly.Infrastructure.Persistence.Configurations;

public sealed class TelemetryReadingConfiguration : IEntityTypeConfiguration<TelemetryReading>
{
    /// <summary>Telemetri tablosunun adı — toplu yazma (COPY) da bunu kullanır.</summary>
    public const string TabloAdi = "telemetry_readings";

    public void Configure(EntityTypeBuilder<TelemetryReading> builder)
    {
        builder.ToTable(TabloAdi);

        builder.HasKey(t => t.Id);

        // TelemetryReading'in bütün alanları salt okunur ({ get; }). EF Core'un
        // varsayılan kuralı salt okunur özellikleri EŞLEMEZ — Ride.StartedAt'te
        // yaşanan ve aylarca fark edilmeyen hata tam olarak buydu. Bu yüzden
        // burada dördü de açıkça yazılı. EslemeButunluguTests unutulmasını
        // engelliyor.
        builder.Property(t => t.DeviceId)
               .HasConversion(
                   deviceId => deviceId.Value,
                   value => new DeviceId(value))
               .HasColumnName("DeviceId")
               .HasMaxLength(DeviceId.MaxLength)
               .IsRequired();

        builder.Property(t => t.BatteryPercentage)
               .IsRequired();

        builder.Property(t => t.RecordedAt)
               .IsRequired();

        builder.OwnsOne(t => t.Location, location =>
        {
            location.Property(l => l.Latitude).HasColumnName("Latitude");
            location.Property(l => l.Longitude).HasColumnName("Longitude");
        });

        builder.Navigation(t => t.Location).IsRequired();

        // "Bu cihazın son ölçümü" ve "şu cihazın son bir saati" sorgularının
        // tamamı bu indeksten geçiyor. RecordedAt AZALAN: neredeyse her sorgu
        // en yeni kayıtları istiyor, ve azalan indeks onları taramadan veriyor.
        builder.HasIndex(t => new { t.DeviceId, t.RecordedAt })
               .HasDatabaseName("ix_telemetri_cihaz_zaman")
               .IsDescending(false, true);

        // Saklama (retention) politikası ADR 0009'da: konum verisi 90 gün.
        // Silme işi bir arka plan servisine ait; bu indeks onun tarama
        // sorgusunu da karşılıyor.
        builder.HasIndex(t => t.RecordedAt)
               .HasDatabaseName("ix_telemetri_zaman");
    }
}
