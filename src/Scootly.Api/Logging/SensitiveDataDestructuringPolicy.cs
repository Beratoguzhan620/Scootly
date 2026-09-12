// Bu dosyada nullable analizi kapalı.
//
// Gerekçe: Serilog'un IDestructuringPolicy arayüzündeki "out" parametresinin
// nullable işaretlemesi sürümden sürüme değişiyor. Uyuşmayan bir işaretleme
// uyarı üretir ve bu projede uyarılar hata sayılıyor (TreatWarningsAsErrors),
// yani kütüphane sürümü yükseldiğinde derleme kırılırdı. Bu tek metot için
// analizi kapatmak, kütüphane sürümüne bağlı kırılganlığı ortadan kaldırıyor.
#nullable disable

using System.Reflection;
using Serilog.Core;
using Serilog.Events;

namespace Scootly.Api.Logging;

/// <summary>
/// Loglara yazılan nesnelerde hassas alan adlarını maskeler.
/// </summary>
/// <remarks>
/// <para>
/// Loglar genellikle en az korunan veri kaynağıdır: birçok kişinin erişimi olur,
/// uzun süre saklanır, yedeklenir ve çoğu zaman şifrelenmez. Bir token'ın
/// yanlışlıkla loga düşmesi, o token'ın süresi dolana kadar geçerli bir açık
/// demektir — üstelik bu açık, kodu okuyarak değil ancak logları okuyarak
/// fark edilir.
/// </para>
/// <para>
/// Bu kural bir GÜVENLİK AĞI'dır, birincil koruma değil. Birincil koruma,
/// hassas veriyi zaten loglamamaktır. Ağın işlevi, birinin ileride
/// <c>_logger.LogInformation("İstek: {@Request}", request)</c> yazması ve o
/// isteğin içinde bir parola bulunması durumunda zararı sınırlamaktır.
/// </para>
/// </remarks>
public sealed class SensitiveDataDestructuringPolicy : IDestructuringPolicy
{
    private const string Maske = "***";

    /// <summary>
    /// Adı bunlardan birini İÇEREN her özellik maskelenir. Tam eşleşme yerine
    /// "içerir" kullanılıyor: <c>Password</c>, <c>NewPassword</c>,
    /// <c>PasswordHash</c> ve <c>ConfirmPassword</c> tek kuralla kapsanıyor.
    /// </summary>
    private static readonly string[] HassasParcalar =
    [
        "password",
        "parola",
        "secret",
        "sir",
        "token",
        "signingkey",
        "apikey",
        "authorization",
        "connectionstring"
    ];

    public bool TryDestructure(
        object value,
        ILogEventPropertyValueFactory propertyValueFactory,
        out LogEventPropertyValue result)
    {
        result = null;

        if (value is null || propertyValueFactory is null)
        {
            return false;
        }

        var tip = value.GetType();

        // Yalnızca kendi tiplerimize karışıyoruz. Kütüphane tiplerini
        // yansımayla gezmek hem pahalı hem de öngörülemez (bazı özellikler
        // okunduğunda yan etki üretir veya istisna fırlatır).
        if (tip.Namespace is null
            || !tip.Namespace.StartsWith("Scootly", StringComparison.Ordinal))
        {
            return false;
        }

        var ozellikler = tip.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        var loglanacaklar = new List<LogEventProperty>(ozellikler.Length);

        foreach (var ozellik in ozellikler)
        {
            if (!ozellik.CanRead || ozellik.GetIndexParameters().Length > 0)
            {
                continue;
            }

            if (Hassas(ozellik.Name))
            {
                loglanacaklar.Add(new LogEventProperty(ozellik.Name, new ScalarValue(Maske)));
                continue;
            }

            object okunan;
            try
            {
                okunan = ozellik.GetValue(value);
            }
            catch (TargetInvocationException)
            {
                // Bir özelliğin okunması istisna fırlatıyorsa loglama yüzünden
                // uygulamanın çökmesi kabul edilemez. Alanı atlıyoruz.
                continue;
            }

            loglanacaklar.Add(new LogEventProperty(
                ozellik.Name,
                propertyValueFactory.CreatePropertyValue(okunan, destructureObjects: true)));
        }

        result = new StructureValue(loglanacaklar, tip.Name);
        return true;
    }

    private static bool Hassas(string ozellikAdi)
    {
        foreach (var parca in HassasParcalar)
        {
            // OrdinalIgnoreCase — kültüre duyarlı karşılaştırma değil.
            // Türkçe yerel ayarında "I" harfi noktasız "ı"ya dönüşür; kültüre
            // duyarlı bir karşılaştırma "APIKey" gibi bir adı kaçırabilir ve
            // maskeleme sessizce devre dışı kalabilirdi.
            if (ozellikAdi.Contains(parca, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
