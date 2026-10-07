using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using Scootly.Domain.Fleet;
using Scootly.Domain.Geo;
using Scootly.Infrastructure.Identity;
using Xunit;

namespace Scootly.Mvc.IntegrationTests;

[Collection(MvcCollection.Name)]
public sealed class MvcPanelTests
{
    private readonly MvcFactory _factory;

    public MvcPanelTests(MvcFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Anonim_Kullanici_Arac_Listesinde_Girise_Yonlendirilmeli()
    {
        var response = await _factory.CreateBrowserClient().GetAsync("/Vehicles");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/Login", response.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Surucu_Filo_Listesine_Erisemez()
    {
        var driver = await _factory.LoginAsync(ScootlyRoles.Driver);

        var response = await driver.GetAsync("/Vehicles");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Contains("/Account/AccessDenied", response.Headers.Location!.ToString());
    }

    [Theory]
    [InlineData("pageNumber=0&pageSize=0")]
    [InlineData("pageNumber=-5&pageSize=100000")]
    [InlineData("pageNumber=2147483647")]
    public async Task Gecersiz_Sayfa_Parametreleri_Hata_Vermemeli(string query)
    {
        var fieldOperator = await _factory.LoginAsync(ScootlyRoles.FieldOperator);

        var response = await fieldOperator.GetAsync($"/Vehicles?{query}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Saha_Operatoru_Bakimdaki_Araci_Panelden_Hizmete_Dondurebilmeli()
    {
        var vehicle = NewVehicle();
        vehicle.SendToMaintenance(DateTime.UtcNow);
        await _factory.SeedAsync(vehicle);

        var fieldOperator = await _factory.LoginAsync(ScootlyRoles.FieldOperator);
        var token = await MvcFactory.GetAntiforgeryTokenAsync(fieldOperator, "/Vehicles?pageSize=100");

        var response = await fieldOperator.PostAsync($"/Vehicles/ReturnToService/{vehicle.Id}", Form(token));

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal(VehicleStatus.Available, await StatusOfAsync(vehicle.Id));
    }

    [Fact]
    public async Task Antiforgery_Tokeni_Olmadan_Durum_Degistirilemez()
    {
        var vehicle = NewVehicle();
        await _factory.SeedAsync(vehicle);
        var fieldOperator = await _factory.LoginAsync(ScootlyRoles.FieldOperator);

        var response = await fieldOperator.PostAsync($"/Vehicles/SendToMaintenance/{vehicle.Id}", new FormUrlEncodedContent([]));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(VehicleStatus.Available, await StatusOfAsync(vehicle.Id));
    }

    [Fact]
    public async Task Gecersiz_Durum_Gecisi_Hata_Mesajiyla_Donmeli()
    {
        var vehicle = NewVehicle();
        await _factory.SeedAsync(vehicle);
        var fieldOperator = await _factory.LoginAsync(ScootlyRoles.FieldOperator);
        var token = await MvcFactory.GetAntiforgeryTokenAsync(fieldOperator, "/Vehicles?pageSize=100");

        var response = await fieldOperator.PostAsync($"/Vehicles/ReturnToService/{vehicle.Id}", Form(token));
        var page = await fieldOperator.GetStringAsync(response.Headers.Location!.ToString());

        Assert.Contains("alert-danger", page);
        Assert.Equal(VehicleStatus.Available, await StatusOfAsync(vehicle.Id));
    }

    [Fact]
    public async Task Harita_Verisi_Surucuye_Yalnizca_Musait_Araclari_Filo_Ekibine_Hepsini_Gostermeli()
    {
        var available = NewVehicle();
        var inMaintenance = NewVehicle();
        inMaintenance.SendToMaintenance(DateTime.UtcNow);
        await _factory.SeedAsync(available, inMaintenance);

        var driver = await _factory.LoginAsync(ScootlyRoles.Driver);
        var fieldOperator = await _factory.LoginAsync(ScootlyRoles.FieldOperator);

        var byDriver = await driver.GetFromJsonAsync<List<MapVehicle>>("/Map/Vehicles");
        var byOperator = await fieldOperator.GetFromJsonAsync<List<MapVehicle>>("/Map/Vehicles");

        Assert.Contains(byDriver!, v => v.Id == available.Id);
        Assert.DoesNotContain(byDriver!, v => v.Id == inMaintenance.Id);
        Assert.Contains(byOperator!, v => v.Id == inMaintenance.Id && v.Status == nameof(VehicleStatus.Maintenance));
    }

    [Fact]
    public async Task Harita_Sayfasi_Yalnizca_Hubda_Gecerli_Rolsuz_Token_Vermeli()
    {
        var manager = await _factory.LoginAsync(ScootlyRoles.FleetManager);

        var html = await manager.GetStringAsync("/Map");
        var token = Regex.Match(html, "data-hub-token=\"([^\"]+)\"").Groups[1].Value;
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        Assert.Equal([HubTokenOptions.DefaultAudience], jwt.Audiences);
        Assert.DoesNotContain(jwt.Claims, c => c.Type is "role" or System.Security.Claims.ClaimTypes.Role);
        Assert.Contains("data-regions=", html);
    }

    /// <summary>Anonim kullanıcı genel yetkilendirme politikası gereği önce girişe yönlendirilir; burada oturum açıktır.</summary>
    [Fact]
    public async Task Bilinmeyen_Adres_Duzgun_404_Sayfasi_Gostermeli()
    {
        var driver = await _factory.LoginAsync(ScootlyRoles.Driver);

        var response = await driver.GetAsync("/boyle-bir-sayfa-yok");
        var html = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Contains("Sayfa Bulunamadı", html);
        Assert.DoesNotContain("btn-secondary\">Ana Sayfaya Dön</a>btn-secondary", html);
    }

    [Fact]
    public async Task Ana_Sayfa_Anonimde_Tanitim_Girisli_Kullanicida_Panel_Olmali()
    {
        var anonymous = await _factory.CreateBrowserClient().GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, anonymous.StatusCode);
        Assert.DoesNotContain("Learn about", await anonymous.Content.ReadAsStringAsync());

        var driver = await _factory.LoginAsync(ScootlyRoles.Driver);
        var signedIn = await driver.GetAsync("/");
        Assert.Equal(HttpStatusCode.Redirect, signedIn.StatusCode);
        Assert.Contains("/Dashboard", signedIn.Headers.Location!.ToString());
    }

    [Fact]
    public async Task Surucu_Panelinde_Filo_Kartlari_Gorunmemeli()
    {
        var driver = await _factory.LoginAsync(ScootlyRoles.Driver);

        var html = await driver.GetStringAsync("/Dashboard");

        Assert.DoesNotContain("Aktif Sürüşler", html);
        Assert.DoesNotContain("Düşük Bataryalı Araçlar", html);
    }

    [Fact]
    public async Task Saglik_Kontrolu_Anonim_Erisilebilir_Olmali()
    {
        var response = await _factory.CreateBrowserClient().GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static Vehicle NewVehicle() => new(
        VehicleId.New(), new VehicleModel("Mvc", 25), new GeoPoint(41.0, 29.0), new BatteryLevel(80), DateTime.UtcNow);

    private static FormUrlEncodedContent Form(string antiforgeryToken)
        => new(new Dictionary<string, string> { ["__RequestVerificationToken"] = antiforgeryToken });

    private Task<VehicleStatus> StatusOfAsync(Guid vehicleId)
        => _factory.QueryAsync(db => db.Vehicles.AsNoTracking().Where(v => v.Id == vehicleId).Select(v => v.Status).SingleAsync());

    private sealed record MapVehicle(Guid Id, double Latitude, double Longitude, int BatteryPercentage, string Status);
}
