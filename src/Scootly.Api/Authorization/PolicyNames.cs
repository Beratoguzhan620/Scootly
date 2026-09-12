namespace Scootly.Api.Authorization;

/// <summary>
/// Yetkilendirme politikalarının adları.
/// </summary>
/// <remarks>
/// Serbest metin yerine sabit kullanılıyor. Politika adını yanlış yazmak
/// derleme hatası vermez; çalışma zamanında "böyle bir politika yok" diye
/// istisna fırlatır — ve bu istisna yalnızca o uç çağrıldığında ortaya çıkar,
/// yani test edilmemiş bir uçta aylarca fark edilmeyebilir.
/// </remarks>
public static class PolicyNames
{
    /// <summary>Filo yöneticisi: araç ekleme, çıkarma, bakıma gönderme.</summary>
    public const string SadeceYonetici = "SadeceYonetici";

    /// <summary>Saha operatörü: araç toplama, şarj, yerinde bakım.</summary>
    public const string SadeceOperator = "SadeceOperator";

    /// <summary>Sürücü: araç kiralama ve sürüş.</summary>
    public const string SadeceSurucu = "SadeceSurucu";

    /// <summary>Denetçi: yalnızca okuma.</summary>
    public const string SadeceDenetci = "SadeceDenetci";

    /// <summary>
    /// Bölgeye bağlı personel — ROL DEĞİL, İDDİA (claim) tabanlı.
    /// Kullanıcının token'ında bir <c>home_region</c> iddiası bulunmasını şart koşar.
    /// </summary>
    /// <remarks>
    /// Rol politikası "sen kimsin" sorusuna cevap verir; iddia politikası
    /// "hangi niteliğe sahipsin" sorusuna. Bu politika bölgenin DEĞERİNE bakmaz,
    /// yalnızca var olduğuna bakar. Değeri erişilen kaynakla karşılaştırmak
    /// üçüncü bir adımdır (kaynak tabanlı yetki) ve 24. günde gelecek.
    /// </remarks>
    public const string BolgeliPersonel = "BolgeliPersonel";
}
