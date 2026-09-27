using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace Scootly.PaymentSimulator;

public sealed class WebhookOptions
{
    public const string SectionName = "Webhook";

    /// <summary>Boşsa webhook gönderilmez.</summary>
    public string? CallbackUrl { get; init; }

    /// <summary>Scootly API ile paylaşılan HMAC anahtarı (user-secrets / ortam değişkeni).</summary>
    [MinLength(32)]
    public string? Secret { get; init; }
}

/// <summary>
/// Gerçek ödeme sağlayıcıları gibi, sonucu ayrıca imzalı bir webhook ile bildirir.
/// İmza biçimi: <c>X-Scootly-Signature: t=&lt;unix&gt;,v1=&lt;hex(HMAC-SHA256(secret, "t.gövde"))&gt;</c>.
/// </summary>
public sealed class WebhookSender
{
    public const string SignatureHeaderName = "X-Scootly-Signature";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly WebhookOptions _options;
    private readonly ILogger<WebhookSender> _logger;

    public WebhookSender(IHttpClientFactory httpClientFactory, IOptions<WebhookOptions> options, ILogger<WebhookSender> logger)
    {
        _httpClientFactory = httpClientFactory;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsEnabled => !string.IsNullOrWhiteSpace(_options.CallbackUrl) && !string.IsNullOrWhiteSpace(_options.Secret);

    public async Task SendAsync(AuthorizeResponse result, decimal amount, string idempotencyKey, CancellationToken cancellationToken)
    {
        if (!IsEnabled)
            return;

        var payload = JsonSerializer.Serialize(new
        {
            EventId = Guid.NewGuid(),
            result.RideId,
            result.Success,
            Amount = amount,
            result.Message,
            IdempotencyKey = idempotencyKey
        }, JsonOptions);

        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        using var request = new HttpRequestMessage(HttpMethod.Post, _options.CallbackUrl)
        {
            Content = new StringContent(payload, Encoding.UTF8, "application/json")
        };
        request.Headers.Add(SignatureHeaderName, CreateSignatureHeader(_options.Secret!, timestamp, payload));

        // Basit üstel geri çekilmeli yeniden deneme: sağlayıcılar webhook'u başarılı olana kadar tekrar gönderir.
        for (var attempt = 1; attempt <= 3; attempt++)
        {
            try
            {
                using var clone = await CloneAsync(request);
                using var response = await _httpClientFactory.CreateClient("webhooks").SendAsync(clone, cancellationToken);

                if (response.IsSuccessStatusCode)
                    return;

                _logger.LogWarning("Webhook reddedildi ({StatusCode}), deneme {Attempt}.", (int)response.StatusCode, attempt);
            }
            catch (HttpRequestException ex)
            {
                _logger.LogWarning(ex, "Webhook gönderilemedi, deneme {Attempt}.", attempt);
            }

            await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)), cancellationToken);
        }
    }

    public static string CreateSignatureHeader(string secret, long unixTimestamp, string body)
    {
        var signedPayload = $"{unixTimestamp.ToString(CultureInfo.InvariantCulture)}.{body}";
        var signature = HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(signedPayload));

        return $"t={unixTimestamp.ToString(CultureInfo.InvariantCulture)},v1={Convert.ToHexStringLower(signature)}";
    }

    private static async Task<HttpRequestMessage> CloneAsync(HttpRequestMessage original)
    {
        var clone = new HttpRequestMessage(original.Method, original.RequestUri)
        {
            Content = new StringContent(await original.Content!.ReadAsStringAsync(), Encoding.UTF8, "application/json")
        };

        foreach (var header in original.Headers)
            clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        return clone;
    }
}
