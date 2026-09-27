using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Scootly.Application.Abstractions;

namespace Scootly.Infrastructure.Payments;

/// <summary>
/// Webhook imzası: <c>X-Scootly-Signature: t=&lt;unix-saniye&gt;,v1=&lt;hex(HMAC-SHA256(secret, "t.hamGövde"))&gt;</c>.
/// İmza ham istek gövdesi üzerinden hesaplanır; zaman damgası tekrar oynatma saldırılarını sınırlar.
/// </summary>
public sealed class PaymentWebhookValidator
{
    public const string SignatureHeaderName = "X-Scootly-Signature";

    private readonly PaymentWebhookOptions _options;
    private readonly IClock _clock;

    public PaymentWebhookValidator(IOptions<PaymentWebhookOptions> options, IClock clock)
    {
        _options = options.Value;
        _clock = clock;
    }

    public bool IsValid(string rawBody, string? signatureHeader)
    {
        if (string.IsNullOrEmpty(_options.Secret) || !TryParseHeader(signatureHeader, out var timestamp, out var providedSignature))
            return false;

        var now = new DateTimeOffset(_clock.UtcNow, TimeSpan.Zero).ToUnixTimeSeconds();

        if (Math.Abs(now - timestamp) > _options.ToleranceSeconds)
            return false;

        var expectedSignature = ComputeSignature(_options.Secret, timestamp, rawBody);

        return CryptographicOperations.FixedTimeEquals(expectedSignature, providedSignature);
    }

    public static string CreateHeaderValue(string secret, long unixTimestamp, string rawBody)
        => $"t={unixTimestamp.ToString(CultureInfo.InvariantCulture)},v1={Convert.ToHexStringLower(ComputeSignature(secret, unixTimestamp, rawBody))}";

    private static byte[] ComputeSignature(string secret, long unixTimestamp, string rawBody)
    {
        var signedPayload = $"{unixTimestamp.ToString(CultureInfo.InvariantCulture)}.{rawBody}";
        return HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(signedPayload));
    }

    private static bool TryParseHeader(string? header, out long timestamp, out byte[] signature)
    {
        timestamp = 0;
        signature = [];

        if (string.IsNullOrWhiteSpace(header))
            return false;

        string? timestampPart = null;
        string? signaturePart = null;

        foreach (var part in header.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (part.StartsWith("t=", StringComparison.Ordinal))
                timestampPart = part[2..];
            else if (part.StartsWith("v1=", StringComparison.Ordinal))
                signaturePart = part[3..];
        }

        if (!long.TryParse(timestampPart, NumberStyles.None, CultureInfo.InvariantCulture, out timestamp) || signaturePart is null)
            return false;

        try
        {
            signature = Convert.FromHexString(signaturePart);
            return signature.Length == HMACSHA256.HashSizeInBytes;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
