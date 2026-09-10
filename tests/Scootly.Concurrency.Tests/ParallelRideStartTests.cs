using Microsoft.Extensions.DependencyInjection;
using Scootly.Domain.Fleet;
using Scootly.Domain.Geo;
using Scootly.Infrastructure.Persistence;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Xunit;

namespace Scootly.Concurrency.Tests;

public sealed class ParallelRideStartTests : IClassFixture<ConcurrencyTestFactory>
{
    private readonly ConcurrencyTestFactory _factory;

    public ParallelRideStartTests(ConcurrencyTestFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Ayni_Araca_50_Paralel_Rezervasyon_Istegi_Yalnizca_Birini_Basarili_Kilmali()
    {
        var vehicleId = await SeedAvailableVehicleAsync();

        var tasks = Enumerable.Range(0, 50).Select(async _ =>
        {
            var client = _factory.CreateClient();
            var token = await RegisterAndLoginAsync(client);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

            var response = await client.PostAsync($"/api/v1/vehicles/{vehicleId}/reserve", null);
            return response.StatusCode;
        });

        var results = await Task.WhenAll(tasks);

        var successCount = results.Count(status => status == HttpStatusCode.OK);

        Assert.Equal(1, successCount);
    }

    private async Task<string> RegisterAndLoginAsync(HttpClient client)
    {
        var email = $"concurrency-test-{Guid.NewGuid()}@scootly.com";
        var password = "test123";

        await client.PostAsJsonAsync("/api/auth/register", new { Email = email, Password = password });

        var loginResponse = await client.PostAsJsonAsync("/api/auth/login", new { Email = email, Password = password });
        var result = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>();

        return result!.Token;
    }

    private async Task<Guid> SeedAvailableVehicleAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ScootlyDbContext>();

        var vehicle = new Vehicle(
            VehicleId.New(),
            new VehicleModel("Xiaomi", 25),
            new GeoPoint(41.0, 29.0),
            new BatteryLevel(80));

        dbContext.Vehicles.Add(vehicle);
        await dbContext.SaveChangesAsync();

        return vehicle.Id;
    }

    private sealed record TokenResponse(string Token);
}