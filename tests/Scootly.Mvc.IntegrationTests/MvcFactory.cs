using System.Net;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Scootly.Infrastructure.Identity;
using Scootly.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace Scootly.Mvc.IntegrationTests;

/// <summary>
/// Gerçek PostgreSQL üzerinde Mvc test sunucusu. Production benzeri ortamda (Development değil) çalışır;
/// Secure cookie'ler gönderilebilsin diye istemci https temel adresi kullanır.
/// </summary>
public sealed partial class MvcFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Password = "MvcTestParola1";

    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder("postgres:16")
        .WithDatabase("scootly_mvc_test")
        .WithUsername("postgres")
        .WithPassword("test_sifre")
        .Build();

    public static readonly string HubKey = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:DefaultConnection", _postgres.GetConnectionString());
        builder.UseSetting("Jwt:HubKey", HubKey);
        builder.UseSetting("Redis:ConnectionString", string.Empty);
        builder.UseSetting("Messaging:Enabled", "false");
    }

    public async ValueTask InitializeAsync()
    {
        await _postgres.StartAsync();

        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ScootlyDbContext>().Database.MigrateAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgres.DisposeAsync();
    }

    public HttpClient CreateBrowserClient() => CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri("https://localhost"),
        HandleCookies = true
    });

    public async Task<string> CreateUserAsync(params string[] roles)
    {
        var email = $"mvc-{Guid.NewGuid():N}@scootly.test";

        using var scope = Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
        var user = new ApplicationUser { UserName = email, Email = email };

        var created = await userManager.CreateAsync(user, Password);
        if (!created.Succeeded)
            throw new InvalidOperationException(string.Join("; ", created.Errors.Select(e => e.Description)));

        foreach (var role in roles)
            await userManager.AddToRoleAsync(user, role);

        return email;
    }

    /// <summary>Giriş formunu antiforgery token'ıyla gönderir; dönen istemci oturum cookie'sini taşır.</summary>
    public async Task<HttpClient> LoginAsync(params string[] roles)
    {
        var email = await CreateUserAsync(roles);
        var client = CreateBrowserClient();

        var token = await GetAntiforgeryTokenAsync(client, "/Account/Login");
        var response = await client.PostAsync("/Account/Login", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["Email"] = email,
            ["Password"] = Password,
            ["__RequestVerificationToken"] = token
        }));

        if (response.StatusCode != HttpStatusCode.Redirect)
            throw new InvalidOperationException($"Giriş başarısız: {(int)response.StatusCode}");

        return client;
    }

    public async Task SeedAsync(params object[] entities)
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ScootlyDbContext>();

        db.AddRange(entities);
        await db.SaveChangesAsync();
    }

    public async Task<T> QueryAsync<T>(Func<ScootlyDbContext, Task<T>> query)
    {
        using var scope = Services.CreateScope();
        return await query(scope.ServiceProvider.GetRequiredService<ScootlyDbContext>());
    }

    public static async Task<string> GetAntiforgeryTokenAsync(HttpClient client, string path)
    {
        var html = await client.GetStringAsync(path);
        var match = AntiforgeryTokenPattern().Match(html);

        return match.Success
            ? WebUtility.HtmlDecode(match.Groups[1].Value)
            : throw new InvalidOperationException($"{path} sayfasında antiforgery token'ı bulunamadı.");
    }

    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex AntiforgeryTokenPattern();
}

[CollectionDefinition(Name)]
public sealed class MvcCollection : ICollectionFixture<MvcFactory>
{
    public const string Name = "mvc";
}
