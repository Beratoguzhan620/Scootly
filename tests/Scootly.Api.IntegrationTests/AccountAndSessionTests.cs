using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Scootly.Api.Contracts.Responses;
using Scootly.Application.Abstractions;
using Scootly.Application.Payments.Commands;
using Scootly.Domain.Riding;
using Scootly.Infrastructure.Identity;
using Scootly.Testing;
using Xunit;

namespace Scootly.Api.IntegrationTests;

/// <summary>Hesap uçları (KVKK), sürüş geçmişi, token iptali (güvenlik damgası) ve hub token'ının kapsamı.</summary>
[Collection(ApiCollection.Name)]
public sealed class AccountAndSessionTests
{
    private const string NewPassword = "YeniParola456";

    private readonly ScootlyApiFactory _factory;

    public AccountAndSessionTests(ScootlyApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Hesap_Bilgisi_Kullanicinin_Kendisini_Dondurmeli()
    {
        var driver = await _factory.CreateDriverClientAsync();

        var account = await driver.Client.GetFromJsonAsync<AccountResponse>("/api/account");

        Assert.Equal(driver.UserId, account!.Id);
        Assert.Equal(driver.Email, account.Email);
        Assert.Equal([ScootlyRoles.Driver], account.Roles);
    }

    [Fact]
    public async Task Cihaz_Tokeni_Hesap_Uclarina_Erisemez()
    {
        var device = await _factory.CreateDeviceClientAsync();

        var response = await device.GetAsync("/api/account");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Parola_Degisince_Eski_Token_Gecersiz_Yeni_Token_Gecerli_Olmali()
    {
        var driver = await _factory.CreateDriverClientAsync();

        var wrongCurrent = await driver.Client.PostAsJsonAsync("/api/account/change-password",
            new { CurrentPassword = "YanlisParola1", NewPassword });
        Assert.Equal(HttpStatusCode.BadRequest, wrongCurrent.StatusCode);

        var changed = await driver.Client.PostAsJsonAsync("/api/account/change-password",
            new { CurrentPassword = TestSecrets.DefaultPassword, NewPassword });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var freshToken = (await changed.Content.ReadFromJsonAsync<TokenResponse>())!.Token;

        var withOldToken = await driver.Client.GetAsync("/api/rides/active");
        Assert.Equal(HttpStatusCode.Unauthorized, withOldToken.StatusCode);

        using var fresh = _factory.CreateClient();
        fresh.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", freshToken);
        var withNewToken = await fresh.GetAsync("/api/account");
        Assert.Equal(HttpStatusCode.OK, withNewToken.StatusCode);

        var loginWithNewPassword = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { driver.Email, Password = NewPassword });
        Assert.Equal(HttpStatusCode.OK, loginWithNewPassword.StatusCode);
    }

    [Fact]
    public async Task Rol_Kaldirilinca_Eski_Rolu_Tasiyan_Token_Reddedilmeli()
    {
        var manager = await _factory.CreateUserClientAsync(ScootlyRoles.FleetManager);
        var fieldOperator = await _factory.CreateUserClientAsync(ScootlyRoles.FieldOperator);
        var vehicle = await _factory.SeedVehicleAsync();

        var before = await fieldOperator.Client.PostAsync($"/api/v1/vehicles/{vehicle.Id}/maintenance", null);
        Assert.Equal(HttpStatusCode.NoContent, before.StatusCode);

        var removed = await manager.Client.DeleteAsync($"/api/v1/admin/users/{fieldOperator.UserId}/roles/{ScootlyRoles.FieldOperator}");
        Assert.Equal(HttpStatusCode.NoContent, removed.StatusCode);

        var after = await fieldOperator.Client.PostAsync($"/api/v1/vehicles/{vehicle.Id}/return-to-service", null);
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
    }

    [Fact]
    public async Task Hesap_Silinince_Token_Gecersiz_Olmali_Ve_Surus_Konumlari_Anonimlesmeli()
    {
        var driver = await _factory.CreateDriverClientAsync();
        var rideId = await CompleteAndPayRideAsync(driver);

        var wrongPassword = await driver.Client.SendAsync(DeleteAccount("YanlisParola1"));
        Assert.Equal(HttpStatusCode.Forbidden, wrongPassword.StatusCode);

        var deleted = await driver.Client.SendAsync(DeleteAccount(TestSecrets.DefaultPassword));
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);

        var withOldToken = await driver.Client.GetAsync("/api/rides/active");
        Assert.Equal(HttpStatusCode.Unauthorized, withOldToken.StatusCode);

        var login = await _factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { driver.Email, Password = TestSecrets.DefaultPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);

        var ride = await _factory.WithDbContextAsync(db => db.Rides.AsNoTracking().SingleAsync(r => r.Id == rideId));
        Assert.Null(ride.StartLocation);
        Assert.Null(ride.EndLocation);
        Assert.Equal(PaymentStatus.Paid, ride.PaymentStatus);
    }

    [Fact]
    public async Task Aktif_Rezervasyon_Varken_Hesap_Silinemez()
    {
        var vehicle = await _factory.SeedVehicleAsync();
        var driver = await _factory.CreateDriverClientAsync();
        (await driver.Client.PostAsync($"/api/v1/vehicles/{vehicle.Id}/reserve", null)).EnsureSuccessStatusCode();

        var response = await driver.Client.SendAsync(DeleteAccount(TestSecrets.DefaultPassword));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Odenmemis_Surus_Varken_Hesap_Silinemez()
    {
        var vehicle = await _factory.SeedVehicleAsync();
        var driver = await _factory.CreateDriverClientAsync();

        (await driver.Client.PostAsync($"/api/v1/vehicles/{vehicle.Id}/reserve", null)).EnsureSuccessStatusCode();
        var start = await driver.Client.PostAsJsonAsync("/api/rides/start", new { VehicleId = vehicle.Id });
        var rideId = (await start.Content.ReadFromJsonAsync<StartRideResponse>())!.RideId;
        (await driver.Client.PostAsJsonAsync($"/api/rides/{rideId}/complete", new { EndLatitude = 41.0, EndLongitude = 29.0 })).EnsureSuccessStatusCode();

        var response = await driver.Client.SendAsync(DeleteAccount(TestSecrets.DefaultPassword));

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Surus_Gecmisi_Yalnizca_Kendi_Suruslerini_Yeniden_Eskiye_Dondurmeli()
    {
        var driver = await _factory.CreateDriverClientAsync();
        var other = await _factory.CreateDriverClientAsync();

        var first = await CompleteAndPayRideAsync(driver);
        var second = await CompleteAndPayRideAsync(driver);
        await CompleteAndPayRideAsync(other);

        var history = await driver.Client.GetFromJsonAsync<PagedResult<RideResponse>>("/api/rides?pageSize=10");

        Assert.Equal(2, history!.TotalCount);
        Assert.Equal([second, first], history.Items.Select(r => r.Id));
        Assert.All(history.Items, r => Assert.Equal(driver.UserId, r.DriverId));

        var invalid = await driver.Client.GetAsync("/api/rides?pageSize=0");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task Hub_Tokeni_Yalnizca_Hubda_Gecerli_Olmali()
    {
        var driver = await _factory.CreateDriverClientAsync();
        var hubToken = await CreateHubTokenAsync(driver.UserId);

        using var client = _factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", hubToken);

        var negotiate = await client.PostAsync("/hubs/fleet/negotiate?negotiateVersion=1", null);
        var api = await client.GetAsync("/api/rides/active");

        Assert.Equal(HttpStatusCode.OK, negotiate.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, api.StatusCode);
    }

    [Fact]
    public async Task Normal_Api_Tokeni_Hubda_Da_Gecerli_Olmali()
    {
        var driver = await _factory.CreateDriverClientAsync();

        var negotiate = await driver.Client.PostAsync("/hubs/fleet/negotiate?negotiateVersion=1", null);

        Assert.Equal(HttpStatusCode.OK, negotiate.StatusCode);
    }

    private async Task<string> CreateHubTokenAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var user = (await scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>().FindByIdAsync(userId.ToString()))!;
        var options = scope.ServiceProvider.GetRequiredService<IOptions<HubTokenOptions>>();

        return new HubTokenGenerator(options, scope.ServiceProvider.GetRequiredService<IClock>()).Generate(user);
    }

    private async Task<Guid> CompleteAndPayRideAsync(AuthenticatedClient driver)
    {
        var vehicle = await _factory.SeedVehicleAsync();

        (await driver.Client.PostAsync($"/api/v1/vehicles/{vehicle.Id}/reserve", null)).EnsureSuccessStatusCode();
        var start = await driver.Client.PostAsJsonAsync("/api/rides/start", new { VehicleId = vehicle.Id });
        start.EnsureSuccessStatusCode();
        var rideId = (await start.Content.ReadFromJsonAsync<StartRideResponse>())!.RideId;

        (await driver.Client.PostAsJsonAsync($"/api/rides/{rideId}/complete", new { EndLatitude = 41.001, EndLongitude = 29.001 })).EnsureSuccessStatusCode();

        using var scope = _factory.Services.CreateScope();
        var charge = await scope.ServiceProvider.GetRequiredService<ChargeRideCommandHandler>().Handle(new ChargeRideCommand(rideId));
        Assert.Equal(ChargeOutcome.Approved, charge.Value);

        return rideId;
    }

    private static HttpRequestMessage DeleteAccount(string password) => new(HttpMethod.Delete, "/api/account")
    {
        Content = JsonContent.Create(new { Password = password })
    };
}
