using System.Globalization;
using System.Text;

namespace Scootly.Infrastructure.Messaging;

/// <summary>Mesaj başlıkları.</summary>
/// <remarks>
/// <para>
/// Deneme sayısını kendi başlığımızda tutuyoruz, RabbitMQ'nun <c>x-death</c>
/// başlığında değil. <c>x-death</c>'in içeriği RabbitMQ sürümüne göre değişti
/// (4.x'te istemcinin gönderdiği <c>x-</c> önekli başlıklar da farklı ele
/// alınıyor) ve onu okumak, sayacın kuyruk yapılandırmasına göre sessizce
/// sıfırda kalması riskini taşıyor. Kendi başlığımız her durumda aynı.
/// </para>
/// <para>
/// Önek <c>scootly-</c>, <c>x-</c> değil: <c>x-</c> RabbitMQ'nun kendisine
/// ayrılmış.
/// </para>
/// </remarks>
public static class MessageHeaders
{
    /// <summary>Bu teslimin kaçıncı deneme olduğu; ilk yayınlamada 1.</summary>
    public const string Attempt = "scootly-attempt";

    /// <summary>Son başarısızlığın sebebi (yönetim arayüzünde okunabilsin diye).</summary>
    public const string FailureReason = "scootly-failure-reason";

    /// <summary>Başarısızlığın türü: Poison, Rejected, Transient.</summary>
    public const string FailureKind = "scootly-failure-kind";

    /// <summary>Mesajın işlenemediği kuyruk.</summary>
    public const string FailedQueue = "scootly-failed-queue";

    public static int ReadAttempt(IDictionary<string, object?>? headers)
    {
        if (headers is null || !headers.TryGetValue(Attempt, out var deger) || deger is null)
            return 1;

        var sayi = deger switch
        {
            int i => i,
            long l => (int)l,
            short s => s,
            byte b => b,
            byte[] metin when int.TryParse(Encoding.UTF8.GetString(metin), NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) => n,
            string metin when int.TryParse(metin, NumberStyles.Integer, CultureInfo.InvariantCulture, out var n) => n,
            _ => 1
        };

        return Math.Max(1, sayi);
    }
}
