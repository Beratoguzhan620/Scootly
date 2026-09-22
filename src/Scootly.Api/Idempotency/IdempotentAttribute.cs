namespace Scootly.Api.Idempotency;

/// <summary>
/// Bir ucun tekrara dayanıklı olması gerektiğini işaretler
/// (60. gün deneyi — ÖZNİTELİK yaklaşımı).
/// </summary>
/// <remarks>
/// <para>
/// Bu öznitelik ve <see cref="IdempotencyFilter"/>, 60. günün deneyinin bir
/// yarısı. Diğer yarısı <c>IIdempotentCommand</c> arayüzü. İkisi aynı işi
/// yapıyor; hangisinin bu projeye uygun olduğu ADR 0020'de karşılaştırıldı ve
/// karara bağlandı.
/// </para>
/// <para>
/// <b>Deney amaçlı yazıldı, hiçbir uca uygulanmadı.</b> Silinmedi çünkü
/// karşılaştırmanın "öznitelik" tarafı olmadan ADR 0020 okunabilir bir belge
/// değil, bir iddia listesi olurdu.
/// </para>
/// </remarks>
[AttributeUsage(AttributeTargets.Method | AttributeTargets.Class, AllowMultiple = false)]
public sealed class IdempotentAttribute : Attribute
{
    /// <summary>Anahtarın taşındığı istek başlığı.</summary>
    public const string BaslikAdi = "Idempotency-Key";

    /// <summary>Anahtarın ne kadar süre hatırlanacağı (dakika).</summary>
    public int HatirlamaSuresiDakika { get; init; } = 60;
}
