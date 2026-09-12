using Scootly.Application.Riding.Commands;
using Scootly.Domain.Common;
using Scootly.Infrastructure.Persistence.Repositories;
using Xunit.Abstractions;

namespace Scootly.DbLab;

/// <summary>
/// Gün 38 — iyimser eşzamanlılık. Aynı yarış, bu sefer gerçek handler ve
/// sürüm damgası ile.
/// </summary>
[Collection(LabCollection.Adi)]
public sealed class Gun38_IyimserEszamanlilikTests
{
    private const int EszamanliIstek = 50;

    private readonly LabFixture _lab;
    private readonly ITestOutputHelper _cikti;

    public Gun38_IyimserEszamanlilikTests(LabFixture lab, ITestOutputHelper cikti)
    {
        _lab = lab;
        _cikti = cikti;
    }

    [Fact]
    public async Task Elli_esz_amanli_istekten_tam_olarak_biri_rezervasyon_olusturur()
    {
        Assert.True(_lab.Hazir, _lab.AtlamaSebebi);

        var aracId = await LabVeri.YeniMusaitAracAsync();

        var denemeler = Enumerable.Range(0, EszamanliIstek)
            .Select(_ => RezerveEtAsync(aracId));

        var sonuclar = await Task.WhenAll(denemeler);

        var basarili = sonuclar.Count(s => s.IsSuccess);
        var cakisma = sonuclar.Count(s => !s.IsSuccess && s.Error == ReserveVehicleCommandHandler.CakismaMesaji);
        var kuralIhlali = sonuclar.Count(s => !s.IsSuccess && s.Error != ReserveVehicleCommandHandler.CakismaMesaji);

        _cikti.WriteLine($"{EszamanliIstek} esz aman li istek:");
        _cikti.WriteLine($"  basarili      : {basarili}");
        _cikti.WriteLine($"  cakisma (409) : {cakisma}");
        _cikti.WriteLine($"  kural ihlali  : {kuralIhlali}");
        _cikti.WriteLine($"  son durum     : {await LabVeri.DurumAsync(aracId)}");
        _cikti.WriteLine($"  surum damgasi : {await LabVeri.SurumAsync(aracId)}");
        _cikti.WriteLine(string.Empty);
        _cikti.WriteLine("Iki farkli engelleme calisiyor:");
        _cikti.WriteLine(" - 'cakisma': surum damgasi tuttu. Istek araci Available okudu,");
        _cikti.WriteLine("   ama yazmaya gittiginde damga degismisti.");
        _cikti.WriteLine(" - 'kural ihlali': istek araci zaten Reserved okudu, alan modeli");
        _cikti.WriteLine("   daha veritabanina gitmeden reddetti.");
        _cikti.WriteLine(string.Empty);
        _cikti.WriteLine("Surum damgasinin 1 olmasi onemli: satir TAM OLARAK BIR KEZ");
        _cikti.WriteLine("guncellendi. Koruma olmasaydi 50 kez guncellenirdi.");

        Assert.Equal(1, basarili);
        Assert.Equal("Reserved", await LabVeri.DurumAsync(aracId));
        Assert.Equal(1, await LabVeri.SurumAsync(aracId));
    }

    /// <summary>
    /// Her çağrı KENDİ DbContext'ini açıyor. Tek bir context'i paralel
    /// kullanmak başlı başına bir hata olurdu ve ölçmek istediğimiz yarışı
    /// gizlerdi — gördüğümüz şey veritabanı eşzamanlılığı değil, EF'in kendi
    /// iç durumunun bozulması olurdu.
    /// </summary>
    private static async Task<Result> RezerveEtAsync(Guid aracId)
    {
        await using var db = Lab.Context();

        var handler = new ReserveVehicleCommandHandler(new VehicleRepository(db), db);

        try
        {
            return await handler.Handle(new ReserveVehicleCommand(aracId, Guid.NewGuid()));
        }
        catch (DomainException ex)
        {
            // Araç okunduğunda zaten Reserved ise Vehicle.Reserve() bunu
            // fırlatır — veritabanına hiç gidilmez. Bu da bir savunma katmanı.
            return Result.Failure(ex.Message);
        }
    }
}
