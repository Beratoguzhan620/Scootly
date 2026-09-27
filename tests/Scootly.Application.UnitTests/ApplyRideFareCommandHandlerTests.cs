using Scootly.Application.IntegrationEvents;
using Scootly.Application.Pricing.Commands;
using Scootly.Domain.Geo;
using Scootly.Domain.Pricing;
using Scootly.Domain.Riding;
using Xunit;

namespace Scootly.Application.UnitTests;

public sealed class ApplyRideFareCommandHandlerTests
{
    private static ApplyRideFareCommandHandler Handler(Ride? r, SahteIsBirimi ib)
        => new(new SahteSurusDeposu(r), ib, ib, new SabitSaat());

    private static Ride Tamamlanmis(int dakika)
    {
        var r = new Ride(RideId.New(), Guid.NewGuid(), Guid.NewGuid(), new GeoPoint(41.0, 29.0), Kurgu.Baslangic);
        r.Complete(new GeoPoint(41.0, 29.01), Kurgu.Baslangic.AddMinutes(dakika));
        return r;
    }

    [Fact]
    public async Task Ucret_Yazilir_Ve_Odeme_Istegi_Ayni_Kayitta_Outboxa_Gider()
    {
        var surus = Tamamlanmis(10);
        var ib = new SahteIsBirimi();

        var result = await Handler(surus, ib).Handle(new ApplyRideFareCommand(surus.Id));

        Assert.True(result.IsSuccess);
        Assert.Equal(Tariff.Standard.Calculate(TimeSpan.FromMinutes(10)), surus.Fare);
        Assert.Equal(1, ib.KayitSayisi);
        var istek = Assert.IsType<PaymentAuthorizationRequestedIntegrationEvent>(Assert.Single(ib.KaydedilenOlaylar));
        Assert.Equal(surus.Id, istek.RideId);
        Assert.Equal(surus.DriverId, istek.DriverId);
        Assert.Equal(surus.Fare, istek.Amount);
    }

    [Fact]
    public async Task Ayni_Mesaj_Ikinci_Kez_Gelirse_Ikinci_Odeme_Istegi_Uretilmez()
    {
        var surus = Tamamlanmis(3);
        var ib = new SahteIsBirimi();
        var h = Handler(surus, ib);

        await h.Handle(new ApplyRideFareCommand(surus.Id));
        var ilk = surus.Fare;
        var ikinci = await h.Handle(new ApplyRideFareCommand(surus.Id));

        Assert.True(ikinci.IsSuccess);
        Assert.Equal(ilk, surus.Fare);
        Assert.Single(ib.KaydedilenOlaylar);
    }

    [Fact]
    public async Task Olmayan_Surus_Reddedilir()
    {
        var ib = new SahteIsBirimi();
        Assert.False((await Handler(null, ib).Handle(new ApplyRideFareCommand(Guid.NewGuid()))).IsSuccess);
        Assert.Empty(ib.KaydedilenOlaylar);
    }

    [Fact]
    public async Task Aktif_Surus_Reddedilir()
    {
        var surus = new Ride(RideId.New(), Guid.NewGuid(), Guid.NewGuid(), new GeoPoint(41.0, 29.0), DateTime.UtcNow);
        var ib = new SahteIsBirimi();

        Assert.False((await Handler(surus, ib).Handle(new ApplyRideFareCommand(surus.Id))).IsSuccess);
        Assert.Null(surus.Fare);
    }
}
