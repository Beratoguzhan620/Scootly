using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace Scootly.Infrastructure.Payments;

/// <summary>
/// Ödeme webhook'unun gerçekten ödeme sağlayıcısından geldiğini doğrular (70. gün).
/// </summary>
/// <remarks>
/// <para>
/// <b>İmza:</b> <c>HMAC-SHA256(sır, "{zaman damgası}.{gövde}")</c>, küçük
/// harf onaltılık. İmzasız bir webhook ucu, adresini bilen herkesin "ödeme
/// başarılı" diye sahte bildirim göndermesine açık kapı.
/// </para>
/// <para>
/// <b>Zaman damgası da imzanın içinde.</b> Yalnızca gövde imzalansaydı, bir
/// kez yakalanan geçerli bir istek sonsuza kadar tekrar gönderilebilirdi.
/// Damga imzalı olduğu için değiştirilemiyor; <see cref="PaymentOptions.WebhookToleranceSeconds"/>
/// kadar eskiyse istek reddediliyor. Pencerenin içindeki tekrarları ise
/// idempotency kaydı (68. gün) yakalıyor.
/// </para>
/// <para>
/// <b>Karşılaştırma sabit zamanlı</b> (<see cref="CryptographicOperations.FixedTimeEquals"/>).
/// Sıradan <c>==</c> ilk farklı karakterde durur; yanıt süresini ölçen
/// biri imzayı karakter karakter tahmin edebilirdi.
/// </para>
/// </remarks>
public sealed class PaymentWebhookValidator
{
    private readonly PaymentOptions _options;
    private readonly TimeProvider _time;

    public PaymentWebhookValidator(PaymentOptions options, TimeProvider time)
    {
        _options = options;
        _time = time;
    }

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_options.WebhookSecret);

    public bool IsValid(string? timestampHeader, string? signatureHeader, byte[] body)
    {
        ArgumentNullException.ThrowIfNull(body);

        if (!IsConfigured || string.IsNullOrWhiteSpace(timestampHeader) || string.IsNullOrWhiteSpace(signatureHeader))
            return false;

        if (!long.TryParse(timestampHeader, NumberStyles.None, CultureInfo.InvariantCulture, out var saniye))
            return false;

        var simdi = _time.GetUtcNow().ToUnixTimeSeconds();
        if (Math.Abs(simdi - saniye) > _options.WebhookToleranceSeconds)
            return false;

        var beklenen = Compute(_options.WebhookSecret, timestampHeader, body);

        byte[] gelen;
        try
        {
            gelen = Convert.FromHexString(signatureHeader.Trim());
        }
        catch (FormatException)
        {
            return false;
        }

        return CryptographicOperations.FixedTimeEquals(beklenen, gelen);
    }

    /// <summary>İmzayı hesaplar. Test ve (71. günde) ödeme simülatörü de kullanır.</summary>
    public static byte[] Compute(string secret, string timestamp, byte[] body)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        ArgumentNullException.ThrowIfNull(body);

        var onEk = Encoding.UTF8.GetBytes(timestamp + ".");
        var imzalanan = new byte[onEk.Length + body.Length];
        Buffer.BlockCopy(onEk, 0, imzalanan, 0, onEk.Length);
        Buffer.BlockCopy(body, 0, imzalanan, onEk.Length, body.Length);

        return HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), imzalanan);
    }

    public static string ComputeHex(string secret, string timestamp, byte[] body)
        => Convert.ToHexStringLower(Compute(secret, timestamp, body));
}
