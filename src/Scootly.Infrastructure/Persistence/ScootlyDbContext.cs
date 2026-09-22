using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using Scootly.Application.Abstractions;
using Scootly.Application.Common;
using Scootly.Domain.Fleet;
using Scootly.Domain.Pricing;
using Scootly.Domain.Riding;
using Scootly.Domain.Telemetry;
using Scootly.Infrastructure.Devices;
using Scootly.Infrastructure.Identity;
using Scootly.Infrastructure.Persistence.Configurations;

namespace Scootly.Infrastructure.Persistence;

public sealed class ScootlyDbContext
    : IdentityDbContext<ApplicationUser, ApplicationRole, Guid>, IApplicationDbContext, IUnitOfWork, ITransactionManager
{
    /// <summary>
    /// Identity tablolarının toplandığı şema. Alan tablolarından ayrı tutulmasının
    /// nedeni yetkilendirme: uygulamanın veritabanı kullanıcısına alan tablolarında
    /// okuma/yazma verirken kimlik tablolarında farklı yetki tanımlanabilsin.
    /// </summary>
    public const string IdentitySchema = "identity";

    public ScootlyDbContext(DbContextOptions<ScootlyDbContext> options) : base(options) { }

    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Ride> Rides => Set<Ride>();

    /// <summary>Tarifeler (46. gün).</summary>
    public DbSet<Tariff> Tariffs => Set<Tariff>();

    /// <summary>Telemetri kayıtları (51. gün). Yazma yolu için bkz. TelemetryBulkWriter.</summary>
    public DbSet<TelemetryReading> TelemetryReadings => Set<TelemetryReading>();

    /// <summary>Araç cihazlarının kimlik bilgileri (25. gün).</summary>
    public DbSet<DeviceCredential> DeviceCredentials => Set<DeviceCredential>();

    IQueryable<Vehicle> IApplicationDbContext.Vehicles => Vehicles;
    IQueryable<Ride> IApplicationDbContext.Rides => Rides;
    IQueryable<Tariff> IApplicationDbContext.Tariffs => Tariffs;
    IQueryable<TelemetryReading> IApplicationDbContext.TelemetryReadings => TelemetryReadings;

    public void AddRide(Ride ride)
    {
        Rides.Add(ride);
    }

    /// <summary>
    /// 38. gün — sürüm damgalarını artırır ve EF'in eşzamanlılık istisnasını
    /// Application katmanının tanıdığı tipe çevirir.
    /// </summary>
    /// <remarks>
    /// Çeviri burada yapılıyor ki <c>DbUpdateConcurrencyException</c> —yani bir
    /// EF Core tipi— Application katmanına hiç sızmasın. Handler'lar o istisnayı
    /// doğrudan yakalasaydı Application projesinin EF paketine bağımlı olması
    /// gerekirdi.
    /// </remarks>
    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SurumDamgalariniArtir();

        try
        {
            return await base.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConcurrencyConflictException(
                "Kayit, okundugundan beri baska bir islem tarafindan degistirildi.", ex);
        }
    }

    /// <summary>
    /// Degismis her varligin surum damgasini bir artirir.
    /// </summary>
    /// <remarks>
    /// EF, damgayi kendiliginden artirmaz; yalnizca OKUDUGU degeri UPDATE'in
    /// WHERE kosuluna koyar. Artirma bu metotta yapiliyor, boylece her aggregate
    /// icin ayri ayri yazilmasi gerekmiyor ve biri unutuldugunda sessizce
    /// korumasiz kalmiyor.
    /// </remarks>
    private void SurumDamgalariniArtir()
    {
        foreach (var giris in ChangeTracker.Entries())
        {
            if (giris.State != EntityState.Modified)
            {
                continue;
            }

            if (giris.Metadata.FindProperty(VehicleConfiguration.SurumSutunu) is null)
            {
                continue;
            }

            var ozellik = giris.Property(VehicleConfiguration.SurumSutunu);
            ozellik.CurrentValue = Convert.ToInt32(ozellik.CurrentValue ?? 0) + 1;
        }
    }

    /// <summary>35. gün — açık işlem sınırı.</summary>
    public async Task<ITransactionScope> BeginTransactionAsync(CancellationToken cancellationToken = default)
    {
        var transaction = await Database.BeginTransactionAsync(cancellationToken);
        return new EfTransactionScope(transaction);
    }

    /// <summary>41. gün — takip mekanizması varsayılan olarak KAPALI.</summary>
    /// <remarks>
    /// <para>
    /// Change tracker, okunan her varlığın bir kopyasını bellekte tutar ve
    /// <c>SaveChanges</c> anında hangi alanların değiştiğini bu kopyayla
    /// karşılaştırarak bulur. Yazma senaryosunda bu mekanizmanın tamamı
    /// gereklidir. Okuma senaryosunda ise hepsi israf: iki kat bellek ve
    /// satır başına bir karşılaştırma girdisi, hiç kullanılmayacak.
    /// </para>
    /// <para>
    /// Yaygın çözüm her okuma sorgusuna <c>AsNoTracking()</c> eklemektir.
    /// Onu seçmedik çünkü o yaklaşımda <b>unutmanın cezası sessizdir</b>:
    /// unutulan sorgu çalışmaya devam eder, yalnızca yavaşlar, ve bunu ancak
    /// yük testinde fark edersin. Varsayılanı tersine çevirince unutmanın
    /// cezası gürültülü hale geliyor — takip isteyen bir yol
    /// <c>AsTracking()</c> demeyi unutursa <c>SaveChanges</c> hiçbir şey
    /// yazmaz ve bu, testte hemen görülür.
    /// </para>
    /// <para>
    /// Takipsiz sorgunun YANLIŞ tercih olduğu yer: okuduğun varlığı
    /// değiştirip kaydedecekesen. Bu projede o yol repository'lerden geçiyor
    /// ve ikisi de açıkça <c>AsTracking()</c> diyor.
    /// </para>
    /// </remarks>
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        base.OnConfiguring(optionsBuilder);

        optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
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
    /// Yansıma ile döngü kurmak yerine tek tek yazılmasının nedeni: Identity
    /// ileride bir tablo eklerse döngü onu sessizce yakalar ve migration'a
    /// fark ettirmeden girer; açık liste derleme zamanında görünür kalır.
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
