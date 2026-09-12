using Scootly.Api.Contracts.Requests;
using Scootly.Api.Validators;

namespace Scootly.Api.UnitTests;

/// <summary>
/// 26. gündeki düzeltmenin geri alınmasını yakalar.
/// </summary>
/// <remarks>
/// Bu testler alışılmadık: bir davranışı değil, bir sözleşmenin ŞEKLİNİ
/// sınıyorlar. Gerekçe, düzeltilen açığın doğası. Biri ileride "istemci kendi
/// kimliğini gönderse daha pratik olur" diye düşünüp <c>DriverId</c> alanını
/// geri eklerse, bu hiçbir testi kırmaz — uçlar çalışmaya devam eder, sadece
/// açık geri gelir. Sessizce geri alınabilen bir güvenlik düzeltmesi,
/// yapılmamış sayılır.
/// </remarks>
public sealed class ContractShapeTests
{
    [Fact]
    public void StartRideRequest_surucu_kimligi_tasimamali()
    {
        var alanlar = typeof(StartRideRequest)
            .GetProperties()
            .Select(p => p.Name)
            .ToArray();

        Assert.DoesNotContain("DriverId", alanlar, StringComparer.Ordinal);
        Assert.Contains("VehicleId", alanlar, StringComparer.Ordinal);
    }

    [Fact]
    public void Hicbir_istek_sozlesmesi_DriverId_tasimamali()
    {
        var sozlesmeler = typeof(StartRideRequest).Assembly
            .GetTypes()
            .Where(t => t.Namespace == "Scootly.Api.Contracts.Requests");

        foreach (var sozlesme in sozlesmeler)
        {
            var driverId = sozlesme.GetProperties()
                .FirstOrDefault(p => string.Equals(p.Name, "DriverId", StringComparison.Ordinal));

            Assert.True(
                driverId is null,
                $"{sozlesme.Name} içinde DriverId var. Sürücü kimliği istemciden alınamaz, " +
                "token'dan okunmalı (bkz. docs/security/owasp-taramasi.md).");
        }
    }

    [Fact]
    public void Dogrulayici_arac_kimligi_bos_ise_reddeder()
    {
        var dogrulayici = new StartRideRequestValidator();

        var (gecerli, hata) = dogrulayici.Validate(new StartRideRequest(Guid.Empty));

        Assert.False(gecerli);
        Assert.NotNull(hata);
    }

    [Fact]
    public void Dogrulayici_arac_kimligi_dolu_ise_kabul_eder()
    {
        var dogrulayici = new StartRideRequestValidator();

        var (gecerli, _) = dogrulayici.Validate(new StartRideRequest(Guid.NewGuid()));

        Assert.True(gecerli);
    }

    [Fact]
    public void Sayfa_boyutu_ust_siniri_makul_olmali()
    {
        // Sınırsız sayfa boyutu, kimlik doğrulaması gerektirmeyen bir uçta
        // bedava bir hizmet dışı bırakma aracıdır.
        Assert.InRange(PageRequest.MaxPageSize, 1, 500);
        Assert.InRange(PageRequest.DefaultPageSize, 1, PageRequest.MaxPageSize);
    }
}
