using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scootly.Domain.Fleet;

namespace Scootly.Infrastructure.Persistence.Configurations;

public sealed class VehicleConfiguration : IEntityTypeConfiguration<Vehicle>
{
    public void Configure(EntityTypeBuilder<Vehicle> builder)
    {
        builder.ToTable("Vehicles");

        builder.HasKey(v => v.Id);

        builder.Property(v => v.Status)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.OwnsOne(v => v.Model, model =>
        {
            // 32. gün: 100 karakterden 64'e indirildi. En uzun gerçek marka adı
            // ("Segway Ninebot Max G2") 21 karakter; 64 rahat bir tavan.
            // Sınırın kendisi bir doğrulama noktası: veritabanı, uygulama
            // katmanı atlasa bile 500 karakterlik bir markayı kabul etmez.
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

            // --- 33. gün: konum indeksi ---
            //
            // İndeks BU BLOĞUN İÇİNDE tanımlanmak zorunda. Latitude ve Longitude
            // Vehicle'ın değil, sahip olunan (owned) GeoPoint tipinin özellikleri;
            // aynı tabloya yazılıyor olmaları onları Vehicle'ın özelliği yapmıyor.
            // Dışarıda builder.HasIndex("Latitude", "Longitude") yazıldığında EF
            // bu adları Vehicle üzerinde arar, bulamaz ve tipi belirtilmemiş bir
            // gölge (shadow) özellik oluşturmaya çalışıp hata verir.
            //
            // Bileşik indekste SÜTUN SIRASI önemli: indeks ilk sütuna göre sıralı
            // tutulur. Bu sorguda ikisi de BETWEEN ile süzüldüğü için sıra kritik
            // değil; Latitude önce yazıldı çünkü Adana'da enlem aralığı boylamdan
            // dar, yani daha seçici.
            location.HasIndex(l => new { l.Latitude, l.Longitude })
                    .HasDatabaseName("ix_vehicles_konum");
        });

        builder.Navigation(v => v.Model).IsRequired();
        builder.Navigation(v => v.Battery).IsRequired();
        builder.Navigation(v => v.Location).IsRequired();

        // Kısmi (partial) indeks. Status yalnızca dört değer alıyor — seçiciliği
        // düşük, tek başına indekslemek pek işe yaramaz: planlayıcı satırların
        // dörtte birine gitmek için indeks okumaktansa tabloyu taramayı tercih
        // eder. Ama sorguların çoğu yalnızca müsait araçlarla ilgileniyor;
        // WHERE koşullu indeks yalnızca o satırları tutuyor, yani hem küçük hem
        // isabetli. Status gerçekten Vehicle üzerinde bir özellik olduğu için
        // bu tanım dışarıda durabiliyor.
        builder.HasIndex(v => v.Status)
               .HasDatabaseName("ix_vehicles_durum")
               .HasFilter("\"Status\" = 'Available'");
    }
}
