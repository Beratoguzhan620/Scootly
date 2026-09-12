using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scootly.Infrastructure.Identity;

namespace Scootly.Infrastructure.Persistence.Configurations;

public sealed class ApplicationUserConfiguration : IEntityTypeConfiguration<ApplicationUser>
{
    public void Configure(EntityTypeBuilder<ApplicationUser> builder)
    {
        // Sınırsız metin yerine açık bir üst sınır. PostgreSQL'de text ile
        // varchar(n) arasında performans farkı yok; fark, beklenmeyen boyutta
        // verinin veritabanına hiç giremiyor olması.
        builder.Property(u => u.HomeRegion)
               .HasMaxLength(64);

        // "Şu bölgenin operatörlerini getir" sorgusu 25. günde gelecek.
        // İndeksin adı açıkça verildi; EF'in ürettiği varsayılan ad
        // sütun adı değişirse sessizce değişir ve migration'da gürültü yapar.
        builder.HasIndex(u => u.HomeRegion)
               .HasDatabaseName("ix_users_home_region");
    }
}
