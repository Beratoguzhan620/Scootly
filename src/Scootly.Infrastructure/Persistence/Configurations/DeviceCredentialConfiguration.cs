using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Scootly.Infrastructure.Devices;

namespace Scootly.Infrastructure.Persistence.Configurations;

public sealed class DeviceCredentialConfiguration : IEntityTypeConfiguration<DeviceCredential>
{
    public void Configure(EntityTypeBuilder<DeviceCredential> builder)
    {
        // Kimlik tablolarıyla aynı şemada: bu da bir kimlik bilgisidir ve
        // 21. günde kurduğumuz en az yetki ayrımı buraya da uygulanmalı.
        builder.ToTable("device_credentials", ScootlyDbContext.IdentitySchema);

        builder.HasKey(d => d.DeviceId);

        builder.Property(d => d.DeviceId)
               .HasMaxLength(64)
               .IsRequired();

        builder.Property(d => d.SecretHash)
               .HasMaxLength(512)
               .IsRequired();

        builder.Property(d => d.IsActive)
               .IsRequired();
    }
}
