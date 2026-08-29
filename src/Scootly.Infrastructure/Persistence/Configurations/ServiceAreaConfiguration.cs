using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scootly.Domain.Geo;

namespace Scootly.Infrastructure.Persistence.Configurations;

public sealed class ServiceAreaConfiguration : IEntityTypeConfiguration<ServiceArea>
{
    public void Configure(EntityTypeBuilder<ServiceArea> builder)
    {
        builder.ToTable("ServiceAreas");

        builder.Property<Guid>("Id");
        builder.HasKey("Id");

        builder.Property(a => a.Name).HasMaxLength(200).IsRequired();

        builder.OwnsMany(a => a.Boundary, boundary =>
        {
            boundary.ToTable("ServiceAreaBoundaryPoints");
            boundary.WithOwner().HasForeignKey("ServiceAreaId");
            boundary.Property<int>("Id");
            boundary.HasKey("Id");
            boundary.Property(p => p.Latitude).HasColumnName("Latitude");
            boundary.Property(p => p.Longitude).HasColumnName("Longitude");
        });
    }
}