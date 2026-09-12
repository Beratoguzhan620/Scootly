using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Scootly.Application.Abstractions;
using Scootly.Domain.Fleet;
using Scootly.Domain.Riding;
using Scootly.Infrastructure.Identity;

namespace Scootly.Infrastructure.Persistence;

public sealed class ScootlyDbContext
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>, IApplicationDbContext, IUnitOfWork
{
    /// <summary>
    /// Identity tablolarının toplandığı şema. Alan tablolarından ayrı tutulmasının
    /// nedeni yetkilendirme: 14. günde konuştuğumuz en az yetki prensibiyle,
    /// uygulamanın veritabanı kullanıcısına alan tablolarında okuma/yazma verirken
    /// kimlik tablolarında farklı (örneğin yalnızca okuma) yetki tanımlanabilsin.
    /// </summary>
    public const string IdentitySchema = "identity";

    public ScootlyDbContext(DbContextOptions<ScootlyDbContext> options) : base(options) { }

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Ride> Rides => Set<Ride>();

    IQueryable<Vehicle> IApplicationDbContext.Vehicles => Vehicles;
    IQueryable<Ride> IApplicationDbContext.Rides => Rides;

    public void AddRide(Ride ride)
    {
        Rides.Add(ride);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Identity'nin kendi eşlemeleri ÖNCE uygulanmalı; aşağıdaki satırlar
        // ve ApplyConfigurationsFromAssembly bunların üzerine yazar.
        base.OnModelCreating(modelBuilder);

        MapIdentityTables(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ScootlyDbContext).Assembly);
    }

    /// <summary>
    /// Identity'nin yedi tablosunu ayrı şemaya ve okunabilir adlara taşır.
    /// Yansıma (reflection) ile döngü kurmak yerine tek tek yazılmasının nedeni:
    /// Identity ileride bir tablo eklerse döngü onu sessizce yakalar ve migration'a
    /// fark ettirmeden girer; açık liste ise derleme zamanında görünür kalır.
    /// </summary>
    private static void MapIdentityTables(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ApplicationUser>().ToTable("users", IdentitySchema);
        modelBuilder.Entity<ApplicationRole>().ToTable("roles", IdentitySchema);
        modelBuilder.Entity<IdentityUserRole<Guid>>().ToTable("user_roles", IdentitySchema);
        modelBuilder.Entity<IdentityUserClaim<Guid>>().ToTable("user_claims", IdentitySchema);
        modelBuilder.Entity<IdentityUserLogin<Guid>>().ToTable("user_logins", IdentitySchema);
        modelBuilder.Entity<IdentityUserToken<Guid>>().ToTable("user_tokens", IdentitySchema);
        modelBuilder.Entity<IdentityRoleClaim<Guid>>().ToTable("role_claims", IdentitySchema);
    }
}
