using Scootly.Application.Behaviors;
using Scootly.Domain.Common;
using Xunit;

namespace Scootly.Application.UnitTests;

/// <summary>68. gun — "ayni mesaji iki kez gonder, bir kez islendigini dogrula".</summary>
public sealed class IdempotencyBehaviorTests
{
    [Fact]
    public async Task Ayni_Mesaj_Iki_Kez_Gelirse_Isleyici_Bir_Kez_Calisir()
    {
        var ib = new SahteIsBirimi();
        var davranis = new IdempotencyBehavior(ib, ib);
        var mesaj = Guid.NewGuid();
        var calisma = 0;

        Task<Result> Isleyici(CancellationToken _) { calisma++; return Task.FromResult(Result.Success()); }

        var ilk = await davranis.ExecuteAsync(mesaj, "scootly.fare-calculation", Isleyici);
        var ikinci = await davranis.ExecuteAsync(mesaj, "scootly.fare-calculation", Isleyici);

        Assert.Equal(1, calisma);
        Assert.False(ilk.WasDuplicate);
        Assert.True(ikinci.WasDuplicate);
        Assert.True(ikinci.Result.IsSuccess);
    }

    [Fact]
    public async Task Ayni_Olayi_Farkli_Tuketiciler_Ayri_Ayri_Isler()
    {
        // Anahtar (mesaj, tuketici). Yalnizca mesaj olsaydi ikinci tuketici
        // "zaten islendi" deyip atlardi.
        var ib = new SahteIsBirimi();
        var davranis = new IdempotencyBehavior(ib, ib);
        var mesaj = Guid.NewGuid();
        var calisma = 0;

        Task<Result> Isleyici(CancellationToken _) { calisma++; return Task.FromResult(Result.Success()); }

        await davranis.ExecuteAsync(mesaj, "kuyruk-a", Isleyici);
        await davranis.ExecuteAsync(mesaj, "kuyruk-b", Isleyici);

        Assert.Equal(2, calisma);
    }

    [Fact]
    public async Task Reddedilen_Mesaj_Islendi_Sayilmaz()
    {
        // Is yapilmadiysa "islendi" kaydi da yazilmamali; yoksa duzeltilip
        // tekrar gonderilen mesaj hic islenmezdi.
        var ib = new SahteIsBirimi();
        var davranis = new IdempotencyBehavior(ib, ib);
        var mesaj = Guid.NewGuid();

        var sonuc = await davranis.ExecuteAsync(mesaj, "k", _ => Task.FromResult(Result.Failure("hayir")));

        Assert.False(sonuc.Result.IsSuccess);
        Assert.Empty(ib.KaydedilenIslenenler);
    }

    [Fact]
    public async Task Isleyici_Hicbir_Sey_Kaydetmese_De_Islendi_Kaydi_Yazilir()
    {
        var ib = new SahteIsBirimi();
        var davranis = new IdempotencyBehavior(ib, ib);
        var mesaj = Guid.NewGuid();

        await davranis.ExecuteAsync(mesaj, "k", _ => Task.FromResult(Result.Success()));

        Assert.Contains((mesaj, "k"), ib.KaydedilenIslenenler);
    }

    [Fact]
    public async Task Bos_Mesaj_Kimligi_Reddedilir()
    {
        var ib = new SahteIsBirimi();
        var sonuc = await new IdempotencyBehavior(ib, ib).ExecuteAsync(Guid.Empty, "k", _ => Task.FromResult(Result.Success()));

        Assert.False(sonuc.Result.IsSuccess);
    }
}
