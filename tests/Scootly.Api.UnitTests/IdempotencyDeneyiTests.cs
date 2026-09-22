using Scootly.Application.Abstractions;
using Scootly.Application.Behaviors;

namespace Scootly.Api.UnitTests;

/// <summary>
/// 60. gün deneyi: tekrara dayanıklılık iki yoldan yapıldı — öznitelik ve
/// arayüz. Bu testler arayüz tarafının gerçekten çalıştığını gösteriyor.
/// </summary>
/// <remarks>
/// Karşılaştırma ve karar ADR 0020'de. Buradaki testler o belgedeki
/// iddiaların doğru olduğunu gösteriyor — belge tek başına bir iddia listesi
/// olurdu.
/// </remarks>
public sealed class IdempotencyDeneyiTests
{
    private sealed record SahteKomut(string IdempotencyKey, Guid AracId) : IIdempotentCommand;

    private sealed record BaskaKomut(string IdempotencyKey) : IIdempotentCommand;

    /// <summary>
    /// Bellek içi depo — YALNIZCA TEST İÇİN.
    /// </summary>
    /// <remarks>
    /// Üretimde bu yanlış olurdu: iki API kopyası çalıştığında her biri kendi
    /// kaydını tutar ve aynı isteğin tekrarı diğer kopyaya düşerse yeni
    /// sayılır. 46. günde bellek içi önbellek için öğrenilen dersin aynısı.
    /// </remarks>
    private sealed class BellekIciDepo : IIdempotencyStore
    {
        private readonly HashSet<string> _gorulenler = new(StringComparer.Ordinal);

        public Task<bool> IlkKezMiAsync(
            string key, TimeSpan timeToLive, CancellationToken cancellationToken = default)
            => Task.FromResult(_gorulenler.Add(key));
    }

    [Fact]
    public async Task Ayni_anahtarla_ikinci_cagri_komutu_hic_calistirmaz()
    {
        var davranis = new IdempotencyBehavior(new BellekIciDepo());
        var komut = new SahteKomut("istemci-anahtari-1", Guid.NewGuid());

        var calismaSayisi = 0;

        Task<string> Calistir(SahteKomut _, CancellationToken __)
        {
            calismaSayisi++;
            return Task.FromResult("olusturuldu");
        }

        var birinci = await davranis.CalistirAsync(komut, Calistir, "tekrar");
        var ikinci = await davranis.CalistirAsync(komut, Calistir, "tekrar");

        Assert.Equal("olusturuldu", birinci);
        Assert.Equal("tekrar", ikinci);

        // Asıl iddia bu. Yalnızca dönen değer farklı olsaydı yan etki
        // (rezervasyon) yine iki kez oluşurdu; komutun HİÇ çalışmaması
        // gerekiyor.
        Assert.Equal(1, calismaSayisi);
    }

    [Fact]
    public async Task Farkli_anahtarlar_ayri_islem_sayilir()
    {
        var davranis = new IdempotencyBehavior(new BellekIciDepo());

        var calismaSayisi = 0;

        Task<string> Calistir(SahteKomut _, CancellationToken __)
        {
            calismaSayisi++;
            return Task.FromResult("olusturuldu");
        }

        await davranis.CalistirAsync(new SahteKomut("a", Guid.NewGuid()), Calistir, "tekrar");
        await davranis.CalistirAsync(new SahteKomut("b", Guid.NewGuid()), Calistir, "tekrar");

        Assert.Equal(2, calismaSayisi);
    }

    [Fact]
    public async Task Anahtar_komut_tipiyle_kapsamlaniyor()
    {
        // Aynı anahtar, iki farklı komut tipi → iki ayrı işlem. Kapsamlama
        // olmasaydı, bir istemcinin aynı anahtarı yeniden kullanması alakasız
        // bir komutu sessizce engellerdi.
        var depo = new BellekIciDepo();
        var davranis = new IdempotencyBehavior(depo);

        var calismaSayisi = 0;

        var birinci = await davranis.CalistirAsync<SahteKomut, string>(
            new SahteKomut("ortak", Guid.NewGuid()),
            (_, _) => { calismaSayisi++; return Task.FromResult("tamam"); },
            "tekrar");

        var ikinci = await davranis.CalistirAsync<BaskaKomut, string>(
            new BaskaKomut("ortak"),
            (_, _) => { calismaSayisi++; return Task.FromResult("tamam"); },
            "tekrar");

        Assert.Equal("tamam", birinci);
        Assert.Equal("tamam", ikinci);
        Assert.Equal(2, calismaSayisi);
    }

    [Fact]
    public async Task Bos_anahtar_sessizce_korumasiz_birakmaz()
    {
        var davranis = new IdempotencyBehavior(new BellekIciDepo());

        // Boş anahtar "koruma yok"a dönüşmemeli: istemci koruma istediğini
        // söylemiş ama anahtarı göndermemiş, ve bu bir hata. Sessizce
        // geçirmek, korunduğunu sanan bir istemci üretirdi.
        await Assert.ThrowsAsync<ArgumentException>(() =>
            davranis.CalistirAsync<SahteKomut, string>(
                new SahteKomut("   ", Guid.NewGuid()),
                (_, _) => Task.FromResult("olusturuldu"),
                "tekrar"));
    }
}
