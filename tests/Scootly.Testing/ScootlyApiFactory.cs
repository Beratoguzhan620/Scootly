using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Scootly.Application.Abstractions;
using Scootly.Domain.Fleet;
using Scootly.Infrastructure.Identity;
using Scootly.Infrastructure.Persistence;
using Testcontainers.PostgreSql;
using Xunit;

namespace Scootly.Testing;

/// <summary>
/// Gerçek PostgreSQL (Testcontainers) üzerinde çalışan test sunucusu. Tüm sırlar test başına üretilir;
/// Redis ve RabbitMQ gerekmez (süreç içi önbellek, mesajlaşma kapalı, sahte ödeme sağlayıcısı).
/// </summary>
public class ScootlyApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgresContainer = new PostgreSqlBuilder("postgres:16")
        .WithDatabase("scootly_test")
        .WithUsername("postgres")
        .WithPassword("test_sifre")
        .Build();

    public FakePaymentGateway PaymentGateway { get; } = new();

    public string ConnectionString => _postgresContainer.GetConnectionString();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.UseSetting("ConnectionStrings:DefaultConnection", ConnectionString);
        builder.UseSetting("Jwt:Key", TestSecrets.JwtKey);
        builder.UseSetting("DeviceAuth:ClientId", TestSecrets.DeviceClientId);
        builder.UseSetting("DeviceAuth:ClientSecret", TestSecrets.DeviceClientSecret);
        builder.UseSetting("PaymentWebhook:Secret", TestSecrets.WebhookSecret);
        builder.UseSetting("Messaging:Enabled", "false");
        builder.UseSetting("Redis:ConnectionString", string.Empty);

        // Testler aynı IP'den çok sayıda kullanıcı açar; sınır davranışı ayrı testlerde düşük değerlerle doğrulanır.
        builder.UseSetting("RateLimiting:AnonymousPerMinute", "100000");
        builder.UseSetting("RateLimiting:AuthPerMinute", "100000");
        builder.UseSetting("RateLimiting:UserPerMinute", "100000");
        builder.UseSetting("RateLimiting:DevicePerMinute", "100000");
        builder.UseSetting("RateLimiting:WebhookPerMinute", "100000");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IPaymentGateway>();
            services.AddSingleton<IPaymentGateway>(PaymentGateway);

            ConfigureTestServices(services);
        });
    }

    /// <summary>Türetilmiş fabrikalar ek servis değişikliği yapabilir (örn. sorgu sayacı).</summary>
    protected virtual void ConfigureTestServices(IServiceCollection services)
    {
    }

    public async ValueTask InitializeAsync()
    {
        await _postgresContainer.StartAsync();

        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ScootlyDbContext>().Database.MigrateAsync();
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _postgresContainer.DisposeAsync();
        GC.SuppressFinalize(this);
    }

    public async Task<T> WithDbContextAsync<T>(Func<ScootlyDbContext, Task<T>> action)
    {
        using var scope = Services.CreateScope();
        return await action(scope.ServiceProvider.GetRequiredService<ScootlyDbContext>());
    }

    public Task WithDbContextAsync(Func<ScootlyDbContext, Task> action)
        => WithDbContextAsync(async dbContext =>
        {
            await action(dbContext);
            return true;
        });

    public async Task<Vehicle> SeedVehicleAsync(double latitude = 41.0, double longitude = 29.0, int batteryPercentage = 80)
    {
        var vehicle = TestData.NewVehicle(latitude, longitude, batteryPercentage);

        await WithDbContextAsync(async dbContext =>
        {
            dbContext.Vehicles.Add(vehicle);
            await dbContext.SaveChangesAsync();
        });

        return vehicle;
    }

    /// <summary>Kayıt + giriş akışıyla yeni bir sürücü oluşturur ve token'lı istemci döner.</summary>
    public async Task<AuthenticatedClient> CreateDriverClientAsync()
    {
        var client = CreateClient();
        var email = $"driver-{Guid.NewGuid():N}@scootly.test";

        var register = await client.PostAsJsonAsync("/api/auth/register", new { Email = email, Password = TestSecrets.DefaultPassword });
        register.EnsureSuccessStatusCode();

        return await LoginAsync(client, email);
    }

    /// <summary>Doğrudan kimlik altyapısı üzerinden verilen rollerle kullanıcı oluşturur (ör. FleetManager).</summary>
    public async Task<AuthenticatedClient> CreateUserClientAsync(params string[] roles)
    {
        var email = $"user-{Guid.NewGuid():N}@scootly.test";

        using (var scope = Services.CreateScope())
        {
            var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            var user = new ApplicationUser { UserName = email, Email = email };

            var created = await userManager.CreateAsync(user, TestSecrets.DefaultPassword);
            if (!created.Succeeded)
                throw new InvalidOperationException(string.Join("; ", created.Errors.Select(e => e.Description)));

            foreach (var role in roles)
                await userManager.AddToRoleAsync(user, role);
        }

        return await LoginAsync(CreateClient(), email);
    }

    public async Task<HttpClient> CreateDeviceClientAsync()
    {
        var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/device-auth/token", new
        {
            ClientId = TestSecrets.DeviceClientId,
            ClientSecret = TestSecrets.DeviceClientSecret
        });
        response.EnsureSuccessStatusCode();

        var token = (await response.Content.ReadFromJsonAsync<TokenBody>())!.Token;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        return client;
    }

    private static async Task<AuthenticatedClient> LoginAsync(HttpClient client, string email)
    {
        var login = await client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = TestSecrets.DefaultPassword });
        login.EnsureSuccessStatusCode();

        var token = (await login.Content.ReadFromJsonAsync<TokenBody>())!.Token;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var userId = ReadUserId(token);
        return new AuthenticatedClient(client, userId, email);
    }

    private static Guid ReadUserId(string token)
        => Guid.Parse(new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(token).Subject);

    private sealed record TokenBody(string Token);
}

public sealed record AuthenticatedClient(HttpClient Client, Guid UserId, string Email);
