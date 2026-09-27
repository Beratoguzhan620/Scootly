using System.Net;
using System.Net.Http.Json;
using System.Text;
using Scootly.Api.Contracts.Responses;
using Scootly.Api.Logging;
using Scootly.Api.Contracts.Requests;
using Scootly.Infrastructure.Identity;
using Scootly.Testing;
using Serilog.Core;
using Serilog.Events;
using Xunit;

namespace Scootly.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class PlatformTests
{
    private readonly ScootlyApiFactory _factory;

    public PlatformTests(ScootlyApiFactory factory)
    {
        _factory = factory;
    }

    [Theory]
    [InlineData("/health/live")]
    [InlineData("/health/ready")]
    public async Task Saglik_Kontrolleri_Anonim_Erisilebilir_Olmali(string path)
    {
        var response = await _factory.CreateClient().GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Bozuk_Json_ProblemDetails_Ile_400_Donmeli()
    {
        var driver = await _factory.CreateDriverClientAsync();

        var response = await driver.Client.PostAsync("/api/rides/start", new StringContent("{ bozuk", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task Hizmet_Bolgesi_Yalnizca_Filo_Yoneticisi_Tarafindan_Olusturulabilmeli()
    {
        var manager = await _factory.CreateUserClientAsync(ScootlyRoles.FleetManager);
        var driver = await _factory.CreateDriverClientAsync();
        var name = $"Bölge-{Guid.NewGuid():N}";

        var area = new
        {
            Name = name,
            Boundary = new[]
            {
                new { Latitude = 40.9, Longitude = 28.9 },
                new { Latitude = 40.9, Longitude = 29.2 },
                new { Latitude = 41.2, Longitude = 29.2 },
                new { Latitude = 41.2, Longitude = 28.9 }
            }
        };

        var byDriver = await driver.Client.PostAsJsonAsync("/api/v1/service-areas", area);
        var byManager = await manager.Client.PostAsJsonAsync("/api/v1/service-areas", area);
        var duplicate = await manager.Client.PostAsJsonAsync("/api/v1/service-areas", area);

        Assert.Equal(HttpStatusCode.Forbidden, byDriver.StatusCode);
        Assert.Equal(HttpStatusCode.Created, byManager.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var areas = await _factory.CreateClient().GetFromJsonAsync<List<ServiceAreaResponse>>("/api/v1/service-areas");
        var stored = Assert.Single(areas!, a => a.Name == name);
        Assert.Equal(4, stored.Boundary.Count);
    }

    [Fact]
    public async Task Hub_Anonim_Baglantiyi_Reddetmeli()
    {
        var response = await _factory.CreateClient().PostAsync("/hubs/fleet/negotiate?negotiateVersion=1", null);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}

public sealed class SensitiveDataDestructuringPolicyTests
{
    private sealed class PassthroughFactory : ILogEventPropertyValueFactory
    {
        public LogEventPropertyValue CreatePropertyValue(object? value, bool destructureObjects = false) => new ScalarValue(value);
    }

    [Fact]
    public void Parola_Ozelligi_Maskelenmeli_Eposta_Korunmali()
    {
        var policy = new SensitiveDataDestructuringPolicy();

        var handled = policy.TryDestructure(new LoginRequest("surucu@scootly.test", "GizliParola1"), new PassthroughFactory(), out var result);

        Assert.True(handled);
        var structure = Assert.IsType<StructureValue>(result);
        var properties = structure.Properties.ToDictionary(p => p.Name, p => ((ScalarValue)p.Value).Value);

        Assert.Equal(SensitiveDataDestructuringPolicy.Mask, properties[nameof(LoginRequest.Password)]);
        Assert.Equal("surucu@scootly.test", properties[nameof(LoginRequest.Email)]);
    }

    [Fact]
    public void Hassas_Ozelligi_Olmayan_Nesneler_Varsayilan_Davranisa_Birakilmali()
    {
        var policy = new SensitiveDataDestructuringPolicy();

        var handled = policy.TryDestructure(new VehicleResponse(Guid.NewGuid(), 41, 29, 80, "Available"), new PassthroughFactory(), out _);

        Assert.False(handled);
    }
}
