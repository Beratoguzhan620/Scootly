using System.Net;
using System.Net.Http.Json;
using Scootly.Api.Contracts.Responses;
using Scootly.Infrastructure.Identity;
using Scootly.Infrastructure.Logging;
using Scootly.Testing;
using Xunit;

namespace Scootly.Api.IntegrationTests;

[Collection(ApiCollection.Name)]
public sealed class PlatformHardeningTests
{
    private readonly ScootlyApiFactory _factory;

    public PlatformHardeningTests(ScootlyApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Gecerli_Korelasyon_Kimligi_Yanita_Aynen_Yazilmali()
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, "istemci-123_abc.4:5");

        var response = await _factory.CreateClient().SendAsync(request);

        Assert.Equal("istemci-123_abc.4:5", response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single());
    }

    [Theory]
    [InlineData("yol/ayirici")]
    [InlineData("bosluk iceren")]
    [InlineData("<script>")]
    public async Task Gecersiz_Korelasyon_Kimligi_Yenisiyle_Degistirilmeli(string provided)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.TryAddWithoutValidation(CorrelationIdMiddleware.HeaderName, provided);

        var response = await _factory.CreateClient().SendAsync(request);
        var returned = response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single();

        Assert.NotEqual(provided, returned);
        Assert.True(CorrelationIdMiddleware.IsAcceptable(returned));
    }

    [Fact]
    public async Task Cok_Uzun_Korelasyon_Kimligi_Kabul_Edilmemeli()
    {
        var provided = new string('a', CorrelationIdMiddleware.MaxLength + 1);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/health/live");
        request.Headers.Add(CorrelationIdMiddleware.HeaderName, provided);

        var response = await _factory.CreateClient().SendAsync(request);

        Assert.NotEqual(provided, response.Headers.GetValues(CorrelationIdMiddleware.HeaderName).Single());
    }

    /// <summary>
    /// Sınır noktaları ayrı tabloda tutulduğunda bu senaryoda köşe sırası bozuluyordu (inceleme bulgusu).
    /// Birden fazla, çok noktalı bölge oluşturulur ve okunan sıranın yazılan sırayla aynı olduğu doğrulanır.
    /// </summary>
    [Fact]
    public async Task Hizmet_Bolgesi_Kose_Sirasi_Korunmali()
    {
        var manager = await _factory.CreateUserClientAsync(ScootlyRoles.FleetManager);
        var created = new Dictionary<string, List<BoundaryPointResponse>>();

        for (var area = 0; area < 4; area++)
        {
            var name = $"Sira-{area}-{Guid.NewGuid():N}";
            var boundary = Circle(centerLatitude: 10 + area, centerLongitude: 10 + area, points: 120);

            var response = await manager.Client.PostAsJsonAsync("/api/v1/service-areas", new { Name = name, Boundary = boundary });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);

            created[name] = boundary;
        }

        var areas = await _factory.CreateClient().GetFromJsonAsync<List<ServiceAreaResponse>>("/api/v1/service-areas");

        foreach (var (name, boundary) in created)
        {
            var stored = Assert.Single(areas!, a => a.Name == name);
            Assert.Equal(boundary, stored.Boundary);
        }
    }

    private static List<BoundaryPointResponse> Circle(double centerLatitude, double centerLongitude, int points)
        => Enumerable.Range(0, points)
            .Select(i => 2 * Math.PI * i / points)
            .Select(angle => new BoundaryPointResponse(
                Math.Round(centerLatitude + 0.05 * Math.Sin(angle), 6),
                Math.Round(centerLongitude + 0.05 * Math.Cos(angle), 6)))
            .ToList();
}
