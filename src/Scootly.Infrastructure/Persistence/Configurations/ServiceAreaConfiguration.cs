using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scootly.Application.Common;
using Scootly.Domain.Geo;

namespace Scootly.Infrastructure.Persistence.Configurations;

public sealed class ServiceAreaConfiguration : IEntityTypeConfiguration<ServiceArea>
{
    public void Configure(EntityTypeBuilder<ServiceArea> builder)
    {
        builder.ToTable("ServiceAreas");

        builder.Property<Guid>("Id");
        builder.HasKey("Id");

        builder.Property(a => a.Name).HasMaxLength(ServiceArea.NameMaxLength).IsRequired();

        builder.HasIndex(a => a.Name)
            .HasDatabaseName(ConstraintNames.UniqueServiceAreaName)
            .IsUnique();

        // Poligonun köşe sırası anlamlıdır (ray casting). Noktalar ayrı bir tabloda tutulduğunda EF, bir sahibin
        // alt kayıtlarını sıralamadan okur ve Postgres sırayı bozabilir; bu yüzden sınır tek bir jsonb dizisinde
        // saklanır (dizi sırası korunur). Bkz. ADR 0045.
        builder.OwnsMany(a => a.Boundary, boundary =>
        {
            boundary.ToJson("Boundary");
            boundary.Property(p => p.Latitude).HasJsonPropertyName("Latitude");
            boundary.Property(p => p.Longitude).HasJsonPropertyName("Longitude");
        });

        builder.Navigation(a => a.Boundary).UsePropertyAccessMode(PropertyAccessMode.Field);
    }
}
