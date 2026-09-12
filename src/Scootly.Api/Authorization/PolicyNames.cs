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
    // --- Rol tabanlı: "sen kimsin" ---

    /// <summary>Filo yöneticisi: araç ekleme, çıkarma, bakıma gönderme.</summary>
    public const string SadeceYonetici = "SadeceYonetici";

    /// <summary>Saha operatörü: araç toplama, şarj, yerinde bakım.</summary>
    public const string SadeceOperator = "SadeceOperator";

    /// <summary>Sürücü: araç kiralama ve sürüş.</summary>
    public const string SadeceSurucu = "SadeceSurucu";

    /// <summary>Denetçi: yalnızca okuma.</summary>
    public const string SadeceDenetci = "SadeceDenetci";

    // --- İddia tabanlı: "hangi niteliğe sahipsin" ---

    /// <summary>
    /// Kullanıcının token'ında bir <c>home_region</c> iddiası bulunmasını şart koşar.
    /// Bölgenin DEĞERİNE bakmaz, yalnızca var olduğuna bakar.
    /// </summary>
    public const string BolgeliPersonel = "BolgeliPersonel";

    // --- Kaynak tabanlı: "bu kaynağa sen erişebilir misin" ---

    /// <summary>
    /// Sürüş, isteği yapan kullanıcının kendi sürüşü olmalı.
    /// Karar kaynağın kendisine bakmadan verilemez; bu yüzden
    /// <c>[Authorize]</c> özniteliğiyle değil, controller içinde
    /// <c>IAuthorizationService.AuthorizeAsync(User, ride, ...)</c> ile uygulanır.
    /// </summary>
    public const string SurusSahibi = "SurusSahibi";

    /// <summary>
    /// Operatör yalnızca kendi bölgesindeki kaynağa erişebilir.
    /// Rol kontrolü (operatör mü) ile kaynak kontrolü (aynı bölge mi) birlikte.
    /// </summary>
    public const string OperatorBolgesi = "OperatorBolgesi";
}
