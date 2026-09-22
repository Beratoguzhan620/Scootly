namespace Scootly.Application.Abstractions;

/// <summary>
/// Anahtar-değer önbelleği (47. gün).
/// </summary>
/// <remarks>
/// <para>
/// Arayüz bilinçli olarak dar: <c>GetAsync</c>, <c>SetAsync</c>,
/// <c>RemoveAsync</c>. Redis'in sunduğu her şeyi (küme işlemleri, yayın-abone,
/// sıralı kümeler) buraya taşımak, Application'ı Redis'e bağlamanın uzun
/// yoldan hali olurdu.
/// </para>
/// <para>
/// <c>SetAsync</c>'te yaşam süresi (TTL) <b>zorunlu</b>, isteğe bağlı değil.
/// Süresiz yazılabilen bir önbellek, günün birinde silinmesi unutulmuş bir
/// girdi yüzünden kullanıcıya süresiz bayat veri gösterir. Zorunlu TTL, en
/// kötü durumda bile bayatlığın bir üst sınırı olmasını garanti ediyor —
/// geçersizleştirmeyi unutsak bile (49. gün, bkz. ADR 0017).
/// </para>
/// <para>
/// <b>Asla önbelleklenmemesi gerekenler:</b> kullanıcıya özel veri ortak bir
/// anahtarla, kimlik/yetki kararları, ve para hesabına giren tutarlar. İlki
/// başkasının verisini başkasına gösterir; ikincisi iptal edilmiş bir yetkiyi
/// yaşatır; üçüncüsü bayat fiyattan fatura keser.
/// </para>
/// </remarks>
public interface ICacheService
{
    /// <summary>Anahtarı okur. Yoksa <c>null</c>.</summary>
    Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        where T : class;

    /// <summary>Anahtarı yazar. Yaşam süresi zorunludur.</summary>
    Task SetAsync<T>(string key, T value, TimeSpan timeToLive, CancellationToken cancellationToken = default)
        where T : class;

    /// <summary>Anahtarı siler. Yoksa sessizce geçer.</summary>
    Task RemoveAsync(string key, CancellationToken cancellationToken = default);
}
