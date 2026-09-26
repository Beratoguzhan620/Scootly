using System.Security.Cryptography;
using System.Text;

namespace Scootly.Infrastructure.Payments;

public sealed class PaymentWebhookValidator
{
    private const string SharedSecret = "webhook-paylasilan-gizli-anahtar-2026";

    public bool IsValid(string payload, string providedSignature)
    {
        var expectedSignature = ComputeSignature(payload);
        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedSignature),
            Encoding.UTF8.GetBytes(providedSignature));
    }

    private static string ComputeSignature(string payload)
    {
        var keyBytes = Encoding.UTF8.GetBytes(SharedSecret);
        var payloadBytes = Encoding.UTF8.GetBytes(payload);

        using var hmac = new HMACSHA256(keyBytes);
        var hashBytes = hmac.ComputeHash(payloadBytes);

        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }
}