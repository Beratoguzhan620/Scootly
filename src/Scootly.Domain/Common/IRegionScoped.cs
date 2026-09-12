namespace Scootly.Domain.Common;

/// <summary>
/// Belirli bir operasyon bölgesine ait kaynak.
/// </summary>
/// <remarks>
/// Bölge, bu sistemde bir alan kavramıdır (operasyon bölgesi), bir web kavramı
/// değil — bu yüzden arayüz Domain katmanında duruyor. Api katmanındaki
/// yetkilendirme kuralı bu arayüze bakarak karar veriyor; tersi olsaydı
/// (arayüz Api'de olsaydı) alan sınıflarının web katmanına bakması gerekirdi ve
/// bağımlılık yönü tersine dönerdi.
///
/// İlk gerçek uygulayıcısı <c>FieldTask</c> olacak (saha görevi); o alan modeli
/// henüz yazılmadı. Kural şimdiden yazıldı ve test edildi, çünkü kuralın
/// kendisi kaynağın varlığına bağlı değil.
/// </remarks>
public interface IRegionScoped
{
    /// <summary>Kaynağın ait olduğu bölgenin adı.</summary>
    string Region { get; }
}
