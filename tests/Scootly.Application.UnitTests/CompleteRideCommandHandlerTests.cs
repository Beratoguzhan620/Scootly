using Scootly.Application.IntegrationEvents;
using Scootly.Application.Riding.Commands;
using Scootly.Domain.Fleet;
using Scootly.Domain.Riding;
using Xunit;

namespace Scootly.Application.UnitTests;

public sealed class CompleteRideCommandHandlerTests
{
    [Fact]
    public async Task Aktif_Suruste_Complete_Cagrilinca_Basarili_Olmali()
    {
        var arac = Kurgu.SurusteArac();
        var surus = Kurgu.AktifSurus(arac.Id);
        var ib = new SahteIsBirimi();

        var result = await new CompleteRideCommandHandler(new SahteSurusDeposu(surus), ib, new SabitSaat(), ib)
            .Handle(new CompleteRideCommand(surus.Id, 41.01, 29.01));

        Assert.True(result.IsSuccess);
        Assert.Equal(RideStatus.Completed, surus.Status);
    }

    [Fact]
    public async Task Var_Olmayan_Suruste_Complete_Cagrilinca_Basarisiz_Donmeli()
    {
        var ib = new SahteIsBirimi();

        var result = await new CompleteRideCommandHandler(new SahteSurusDeposu(null), ib, new SabitSaat(), ib)
            .Handle(new CompleteRideCommand(Guid.NewGuid(), 41.01, 29.01));

        Assert.False(result.IsSuccess);
        Assert.Empty(ib.KaydedilenOlaylar);
    }

    [Fact]
    public async Task Olay_Surusle_Ayni_Kayitta_Outboxa_Yazilir()
    {
        // 66. gun: olay dogrudan kuyruga degil, surusle AYNI kayitta outbox'a.
        var arac = Kurgu.SurusteArac();
        var surus = Kurgu.AktifSurus(arac.Id);
        var ib = new SahteIsBirimi();

        await new CompleteRideCommandHandler(new SahteSurusDeposu(surus), ib, new SabitSaat(), ib)
            .Handle(new CompleteRideCommand(surus.Id, 41.01, 29.01));

        Assert.Equal(1, ib.KayitSayisi);
        var olay = Assert.IsType<RideCompletedIntegrationEvent>(Assert.Single(ib.KaydedilenOlaylar));
        Assert.Equal(surus.Id, olay.RideId);
        Assert.Equal(arac.Id, olay.VehicleId);
        olay.Validate();
        Assert.Empty(surus.DomainEvents);
    }

    [Fact]
    public async Task Arac_Odeme_Sonuclanana_Kadar_Suruste_Kalir()
    {
        // 69. gun: araci serbest birakmak saga'nin son adiminin isi.
        var arac = Kurgu.SurusteArac();
        var surus = Kurgu.AktifSurus(arac.Id);
        var ib = new SahteIsBirimi();

        await new CompleteRideCommandHandler(new SahteSurusDeposu(surus), ib, new SabitSaat(), ib)
            .Handle(new CompleteRideCommand(surus.Id, 41.01, 29.01));

        Assert.Equal(VehicleStatus.InRide, arac.Status);
    }
}
