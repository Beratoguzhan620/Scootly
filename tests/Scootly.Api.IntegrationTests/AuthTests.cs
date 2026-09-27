using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Scootly.Infrastructure.Identity;
using Scootly.Testing;
using Xunit;

namespace Scootly.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class AuthTests
{
    private readonly ScootlyApiFactory _factory;

    public AuthTests(ScootlyApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Zayif_Parola_Reddedilmeli()
    {
        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/register", new
        {
            Email = $"weak-{Guid.NewGuid():N}@scootly.test",
            Password = "test123"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Kayitli_Eposta_Tekrar_Kaydedilirse_Hesap_Varligi_Aciga_Cikmamali()
    {
        var driver = await _factory.CreateDriverClientAsync();

        var response = await _factory.CreateClient().PostAsJsonAsync("/api/auth/register", new
        {
            driver.Email,
            Password = TestSecrets.DefaultPassword
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.DoesNotContain(driver.Email, problem!.Detail, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("already", problem.Detail, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Bilinmeyen_Kullanici_Ve_Yanlis_Parola_Ayni_Yaniti_Almali()
    {
        var driver = await _factory.CreateDriverClientAsync();
        var client = _factory.CreateClient();

        var unknown = await client.PostAsJsonAsync("/api/auth/login", new { Email = $"none-{Guid.NewGuid():N}@scootly.test", Password = "YanlisParola1" });
        var wrongPassword = await client.PostAsJsonAsync("/api/auth/login", new { driver.Email, Password = "YanlisParola1" });

        Assert.Equal(HttpStatusCode.Unauthorized, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
    }

    [Fact]
    public async Task Bes_Basarisiz_Denemeden_Sonra_Hesap_Kilitlenmeli()
    {
        var driver = await _factory.CreateDriverClientAsync();
        var client = _factory.CreateClient();

        for (var i = 0; i < 5; i++)
            await client.PostAsJsonAsync("/api/auth/login", new { driver.Email, Password = "YanlisParola1" });

        var withCorrectPassword = await client.PostAsJsonAsync("/api/auth/login", new { driver.Email, Password = TestSecrets.DefaultPassword });

        Assert.Equal(HttpStatusCode.Unauthorized, withCorrectPassword.StatusCode);
    }

    [Fact]
    public async Task Yeni_Kullanici_Surucu_Rolunu_Almali_Ve_Filo_Islemleri_Yapamamali()
    {
        var driver = await _factory.CreateDriverClientAsync();

        var token = driver.Client.DefaultRequestHeaders.Authorization!.Parameter!;
        var roles = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(token).Claims
            .Where(c => c.Type is "role" or System.Security.Claims.ClaimTypes.Role)
            .Select(c => c.Value);

        Assert.Contains(ScootlyRoles.Driver, roles);
        Assert.DoesNotContain(ScootlyRoles.FleetManager, roles);
    }

    [Fact]
    public async Task Filo_Yoneticisi_Kullaniciya_Rol_Atayabilmeli_Surucu_Atayamamali()
    {
        var manager = await _factory.CreateUserClientAsync(ScootlyRoles.FleetManager);
        var driver = await _factory.CreateDriverClientAsync();

        var byDriver = await driver.Client.PostAsync($"/api/v1/admin/users/{driver.UserId}/roles/{ScootlyRoles.FleetManager}", null);
        var byManager = await manager.Client.PostAsync($"/api/v1/admin/users/{driver.UserId}/roles/{ScootlyRoles.FieldOperator}", null);
        var invalidRole = await manager.Client.PostAsync($"/api/v1/admin/users/{driver.UserId}/roles/Admin", null);

        Assert.Equal(HttpStatusCode.Forbidden, byDriver.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, byManager.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, invalidRole.StatusCode);
    }

    [Fact]
    public async Task Giris_Ucu_IP_Basina_Hiz_Sinirina_Takilmali()
    {
        using var limited = _factory.WithWebHostBuilder(builder => builder.UseSetting("RateLimiting:AuthPerMinute", "2"));
        var client = limited.CreateClient();

        HttpResponseMessage? last = null;

        for (var i = 0; i < 3; i++)
            last = await client.PostAsJsonAsync("/api/auth/login", new { Email = "a@b.test", Password = "YanlisParola1" });

        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);
        Assert.True(last.Headers.Contains("Retry-After"));
    }
}
