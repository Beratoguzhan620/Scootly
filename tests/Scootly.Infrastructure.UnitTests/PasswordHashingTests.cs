using Microsoft.AspNetCore.Identity;
using Scootly.Infrastructure.Identity;

namespace Scootly.Infrastructure.UnitTests;

/// <summary>
/// 21. günün asıl iddiasını sınar: parola düz metin olarak saklanmıyor,
/// geri döndürülemez biçimde hash'leniyor ve doğrulama hash üzerinden yapılıyor.
/// Veritabanı gerektirmez — Identity'nin hash bileşeni tek başına çalıştırılabilir.
/// </summary>
public sealed class PasswordHashingTests
{
    private const string DogruParola = "Scootly-Test-2026!";

    private readonly PasswordHasher<ApplicationUser> _hasher = new();
    private readonly ApplicationUser _kullanici = new() { UserName = "test@scootly.local" };

    [Fact]
    public void Hash_duz_metin_parolayi_icermez()
    {
        var hash = _hasher.HashPassword(_kullanici, DogruParola);

        Assert.DoesNotContain(DogruParola, hash);
    }

    [Fact]
    public void Ayni_parola_her_seferinde_farkli_hash_uretir()
    {
        var birinci = _hasher.HashPassword(_kullanici, DogruParola);
        var ikinci = _hasher.HashPassword(_kullanici, DogruParola);

        // Her hash'e rastgele bir tuz (salt) karışır. Tuz olmasaydı aynı parolayı
        // kullanan iki kullanıcı veritabanında aynı satırı gösterirdi ve tek bir
        // önceden hesaplanmış tablo (rainbow table) ikisini birden açardı.
        Assert.NotEqual(birinci, ikinci);
    }

    [Fact]
    public void Dogru_parola_dogrulanir()
    {
        var hash = _hasher.HashPassword(_kullanici, DogruParola);

        var sonuc = _hasher.VerifyHashedPassword(_kullanici, hash, DogruParola);

        Assert.Equal(PasswordVerificationResult.Success, sonuc);
    }

    [Theory]
    [InlineData("scootly-test-2026!")]   // yalnızca büyük/küçük harf farkı
    [InlineData("Scootly-Test-2026")]    // son karakter eksik
    [InlineData("Scootly-Test-2026!!")]  // fazladan karakter
    [InlineData("")]                     // boş
    public void Yanlis_parola_reddedilir(string yanlisParola)
    {
        var hash = _hasher.HashPassword(_kullanici, DogruParola);

        var sonuc = _hasher.VerifyHashedPassword(_kullanici, hash, yanlisParola);

        Assert.Equal(PasswordVerificationResult.Failed, sonuc);
    }
}
