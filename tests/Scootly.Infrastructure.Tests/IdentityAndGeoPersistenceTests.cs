using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Scootly.Application.Abstractions;
using Scootly.Domain.Geo;
using Scootly.Infrastructure.Geo;
using Scootly.Infrastructure.Identity;
using Scootly.Infrastructure.Persistence;
using Xunit;

namespace Scootly.Infrastructure.Tests;

[Collection(InfrastructureCollection.Name)]
public sealed class ServiceAreaPersistenceTests
{
    private readonly InfrastructureFixture _fixture;

    public ServiceAreaPersistenceTests(InfrastructureFixture fixture)
    {
        _fixture = fixture;
    }

    /// <summary>
    /// İnceleme bulgusu: noktalar ayrı tablodayken birden fazla bölge okununca köşe sırası bozuluyordu
    /// (3 bölge x 50 noktada bile). Sınır artık tek bir jsonb dizisinde; sıra korunmalı.
    /// </summary>
    [Fact]
    public async Task Sinir_Noktalari_Yazildiklari_Sirayla_Okunmali()
    {
        var expected = new Dictionary<string, IReadOnlyList<GeoPoint>>();

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ScootlyDbContext>();

            for (var i = 0; i < 5; i++)
            {
                var boundary = Circle(20 + i, 20 + i, 150);
                var area = new ServiceArea($"Kalici-{i}-{Guid.NewGuid():N}", boundary);
                db.ServiceAreas.Add(area);
                expected[area.Name] = boundary;
            }

            await db.SaveChangesAsync();
        }

        using var readScope = _fixture.Services.CreateScope();
        var areas = readScope.ServiceProvider.GetRequiredService<IApplicationDbContext>().ServiceAreas.ToList();

        foreach (var (name, boundary) in expected)
            Assert.Equal(boundary, areas.Single(a => a.Name == name).Boundary);
    }

    [Fact]
    public async Task Bolge_Cozumleyici_Ic_Bukey_Poligonda_Dogru_Bolgeyi_Bulmali()
    {
        // "C" biçimli içbükey poligon: köşe sırası bozulursa boşluktaki nokta yanlışlıkla içeride sayılır.
        var name = $"C-Bolge-{Guid.NewGuid():N}";
        var boundary = new List<GeoPoint>
        {
            new(60.0, 60.0), new(60.0, 63.0), new(61.0, 63.0), new(61.0, 61.0),
            new(62.0, 61.0), new(62.0, 63.0), new(63.0, 63.0), new(63.0, 60.0)
        };

        using (var scope = _fixture.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ScootlyDbContext>();
            db.ServiceAreas.Add(new ServiceArea(name, boundary));
            await db.SaveChangesAsync();
        }

        using var readScope = _fixture.Services.CreateScope();
        var resolver = new ServiceAreaRegionResolver(
            readScope.ServiceProvider.GetRequiredService<ScootlyDbContext>(), new MemoryCache(new MemoryCacheOptions()));

        Assert.Equal(name, await resolver.ResolveRegionAsync(60.5, 61.5));
        Assert.Equal(IRegionResolver.DefaultRegion, await resolver.ResolveRegionAsync(61.5, 62.0));
    }

    private static List<GeoPoint> Circle(double centerLatitude, double centerLongitude, int points)
        => Enumerable.Range(0, points)
            .Select(i => 2 * Math.PI * i / points)
            .Select(angle => new GeoPoint(
                Math.Round(centerLatitude + 0.05 * Math.Sin(angle), 6),
                Math.Round(centerLongitude + 0.05 * Math.Cos(angle), 6)))
            .ToList();
}

[Collection(InfrastructureCollection.Name)]
public sealed class IdentityBootstrapperTests
{
    private const string Password = "IlkYonetici123";

    private readonly InfrastructureFixture _fixture;

    public IdentityBootstrapperTests(InfrastructureFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task Hesap_Yoksa_Filo_Yoneticisi_Olusturulmali_Ve_Tekrar_Calismak_Zararsiz_Olmali()
    {
        using var services = BuildIdentityServices();
        var email = $"ilk-yonetici-{Guid.NewGuid():N}@scootly.test";

        await RunBootstrapperAsync(services, email);
        await RunBootstrapperAsync(services, email);

        using var scope = services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = await userManager.FindByEmailAsync(email);

        Assert.NotNull(user);
        Assert.True(user.EmailConfirmed);
        Assert.Equal([ScootlyRoles.FleetManager], await userManager.GetRolesAsync(user));
    }

    [Fact]
    public async Task Ayni_Epostayla_Kayitli_Mevcut_Hesaba_Yetki_Verilmemeli()
    {
        using var services = BuildIdentityServices();
        var email = $"once-kaydolan-{Guid.NewGuid():N}@scootly.test";

        using (var scope = services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var squatter = new ApplicationUser { UserName = email, Email = email };
            Assert.True((await userManager.CreateAsync(squatter, "SaldirganParola1")).Succeeded);
            await userManager.AddToRoleAsync(squatter, ScootlyRoles.Driver);
        }

        await RunBootstrapperAsync(services, email);

        using var verifyScope = services.CreateScope();
        var manager = verifyScope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var existing = await manager.FindByEmailAsync(email);

        Assert.False(await manager.IsInRoleAsync(existing!, ScootlyRoles.FleetManager));
    }

    private ServiceProvider BuildIdentityServices()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:DefaultConnection"] = _fixture.ConnectionString,
                ["Messaging:Enabled"] = "false"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));
        services.AddScootlyInfrastructure(configuration);
        services.AddDataProtection();
        services.AddScootlyIdentityCore();

        return services.BuildServiceProvider();
    }

    private static Task RunBootstrapperAsync(IServiceProvider services, string email)
    {
        var bootstrapper = new IdentityBootstrapper(
            services,
            Options.Create(new BootstrapOptions { FleetManagerEmail = email, FleetManagerPassword = Password }),
            NullLogger<IdentityBootstrapper>.Instance);

        return bootstrapper.StartAsync(CancellationToken.None);
    }
}
