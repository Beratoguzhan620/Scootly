using Scootly.Application.Billing.Commands;
using Scootly.Application.IntegrationEvents;
using Scootly.Domain.Fleet;
using Scootly.Domain.Riding;
using Xunit;

namespace Scootly.Application.UnitTests;

/// <summary>69. gun — surus bitti → ucret → odeme → arac serbest, ve telafi yolu.</summary>
public sealed class PaymentSagaTests
{
    [Fact]
    public async Task Onaylanan_Odeme_Basarili_Sonuc_Olayi_Uretir()
    {
        var surus = Kurgu.UcretliSurus(Guid.NewGuid(), 35m);
        var ib = new SahteIsBirimi();
        var odeme = new SahteOdeme(onayla: true);

        var r = await new AuthorizeRidePaymentCommandHandler(new SahteSurusDeposu(surus), odeme, ib, ib, new SabitSaat())
            .Handle(new AuthorizeRidePaymentCommand(surus.Id, surus.DriverId, 35m));

        Assert.True(r.IsSuccess);
        var istek = Assert.Single(odeme.Istekler);
        Assert.Equal(surus.Id, istek.IdempotencyKey);
        Assert.Equal(35m, istek.Amount);
        var sonuc = Assert.IsType<PaymentAuthorizedIntegrationEvent>(Assert.Single(ib.KaydedilenOlaylar));
        Assert.True(sonuc.Success);
    }

    [Fact]
    public async Task Reddedilen_Odeme_Hata_Degil_Basarisiz_Sonuc_Olayidir()
    {
        var surus = Kurgu.UcretliSurus(Guid.NewGuid());
        var ib = new SahteIsBirimi();

        var r = await new AuthorizeRidePaymentCommandHandler(new SahteSurusDeposu(surus), new SahteOdeme(false), ib, ib, new SabitSaat())
            .Handle(new AuthorizeRidePaymentCommand(surus.Id, surus.DriverId, 35m));

        Assert.True(r.IsSuccess);
        var sonuc = Assert.IsType<PaymentAuthorizedIntegrationEvent>(Assert.Single(ib.KaydedilenOlaylar));
        Assert.False(sonuc.Success);
        Assert.False(string.IsNullOrWhiteSpace(sonuc.FailureReason));
    }

    [Fact]
    public async Task Odeme_Tutari_Mesajdan_Degil_Surusten_Alinir()
    {
        var surus = Kurgu.UcretliSurus(Guid.NewGuid(), 35m);
        var odeme = new SahteOdeme(true);
        var ib = new SahteIsBirimi();

        await new AuthorizeRidePaymentCommandHandler(new SahteSurusDeposu(surus), odeme, ib, ib, new SabitSaat())
            .Handle(new AuthorizeRidePaymentCommand(surus.Id, surus.DriverId, 999m));

        Assert.Equal(35m, Assert.Single(odeme.Istekler).Amount);
    }

    [Fact]
    public async Task Basarili_Odeme_Surusu_Oder_Ve_Araci_Serbest_Birakir()
    {
        var arac = Kurgu.SurusteArac();
        var surus = Kurgu.UcretliSurus(arac.Id);
        var ib = new SahteIsBirimi();

        var r = await Settle(surus, arac, ib).Handle(new SettleRidePaymentCommand(surus.Id, true, null));

        Assert.True(r.IsSuccess);
        Assert.Equal(RidePaymentStatus.Paid, surus.PaymentStatus);
        Assert.Equal(VehicleStatus.Available, arac.Status);
        Assert.Empty(ib.KaydedilenBorclar);
    }

    [Fact]
    public async Task Basarisiz_Odemede_Telafi_Borc_Acar_Ve_Araci_Yine_De_Serbest_Birakir()
    {
        var arac = Kurgu.SurusteArac();
        var surus = Kurgu.UcretliSurus(arac.Id, 35m);
        var ib = new SahteIsBirimi();

        var r = await Settle(surus, arac, ib).Handle(new SettleRidePaymentCommand(surus.Id, false, "Kart reddedildi."));

        Assert.True(r.IsSuccess);
        Assert.Equal(RidePaymentStatus.PaymentFailed, surus.PaymentStatus);
        Assert.Equal(RideStatus.Completed, surus.Status);        // surus geri ALINMIYOR
        Assert.Equal(VehicleStatus.Available, arac.Status);       // kullanici magdur olmuyor
        var borc = Assert.Single(ib.KaydedilenBorclar);
        Assert.Equal(surus.DriverId, borc.DriverId);
        Assert.Equal(35m, borc.Amount);
    }

    [Fact]
    public async Task Tekrar_Gelen_Sonuc_Araca_Dokunmaz()
    {
        // Ilk sonuc araci serbest birakti ve arac baskasina kiralandi. Gec
        // gelen tekrar mesaj yeni surusu bitirmemeli.
        var arac = Kurgu.SurusteArac();
        var surus = Kurgu.UcretliSurus(arac.Id);
        var ib = new SahteIsBirimi();
        var h = Settle(surus, arac, ib);

        await h.Handle(new SettleRidePaymentCommand(surus.Id, true, null));
        arac.Reserve();
        arac.StartRide();                                        // baskasi kiraladi
        var tekrar = await h.Handle(new SettleRidePaymentCommand(surus.Id, true, null));

        Assert.True(tekrar.IsSuccess);
        Assert.Equal(VehicleStatus.InRide, arac.Status);
    }

    [Fact]
    public async Task Bakima_Alinmis_Araca_Dokunulmaz()
    {
        var arac = Kurgu.SurusteArac();
        var surus = Kurgu.UcretliSurus(arac.Id);
        arac.SendToMaintenance();
        var ib = new SahteIsBirimi();

        await Settle(surus, arac, ib).Handle(new SettleRidePaymentCommand(surus.Id, true, null));

        Assert.Equal(VehicleStatus.Maintenance, arac.Status);
        Assert.Equal(RidePaymentStatus.Paid, surus.PaymentStatus);
    }

    [Fact]
    public async Task Sonuclanmis_Odeme_Icin_Saglayici_Ikinci_Kez_Cagrilmaz()
    {
        var arac = Kurgu.SurusteArac();
        var surus = Kurgu.UcretliSurus(arac.Id);
        var ib = new SahteIsBirimi();
        await Settle(surus, arac, ib).Handle(new SettleRidePaymentCommand(surus.Id, true, null));
        var odeme = new SahteOdeme(true);

        await new AuthorizeRidePaymentCommandHandler(new SahteSurusDeposu(surus), odeme, ib, ib, new SabitSaat())
            .Handle(new AuthorizeRidePaymentCommand(surus.Id, surus.DriverId, 35m));

        Assert.Empty(odeme.Istekler);
    }

    private static SettleRidePaymentCommandHandler Settle(Ride surus, Vehicle arac, SahteIsBirimi ib)
        => new(new SahteSurusDeposu(surus), new SahteAracDeposu(arac), ib, ib, new SabitSaat());
}
