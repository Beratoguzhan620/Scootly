using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scootly.Infrastructure.Identity;

namespace Scootly.Infrastructure.Persistence.Configurations;

/// <summary>Rolleri sabit kimliklerle tohumlar; böylece her ortamda aynı rol kimlikleri bulunur.</summary>
public sealed class ApplicationRoleConfiguration : IEntityTypeConfiguration<ApplicationRole>
{
    public void Configure(EntityTypeBuilder<ApplicationRole> builder)
    {
        builder.HasData(
            CreateRole(ScootlyRoles.DriverRoleId, ScootlyRoles.Driver),
            CreateRole(ScootlyRoles.FleetManagerRoleId, ScootlyRoles.FleetManager),
            CreateRole(ScootlyRoles.FieldOperatorRoleId, ScootlyRoles.FieldOperator));
    }

    private static ApplicationRole CreateRole(Guid id, string name) => new()
    {
        Id = id,
        Name = name,
        NormalizedName = name.ToUpperInvariant(),
        // HasData her migration'da aynı değeri görmeli; bu yüzden sabit.
        ConcurrencyStamp = id.ToString()
    };
}
