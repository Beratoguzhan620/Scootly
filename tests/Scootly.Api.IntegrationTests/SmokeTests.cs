using Xunit;

namespace Scootly.Api.IntegrationTests;

public sealed class SmokeTests : IClassFixture<ScootlyApiFactory>
{
    private readonly ScootlyApiFactory _factory;

    public SmokeTests(ScootlyApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Api_Ayaga_Kalkip_Vehicles_Ucuna_Yanit_Vermeli()
    {
        var client = _factory.CreateClient();

        var response = await client.GetAsync("/api/vehicles");

        response.EnsureSuccessStatusCode();
    }
}