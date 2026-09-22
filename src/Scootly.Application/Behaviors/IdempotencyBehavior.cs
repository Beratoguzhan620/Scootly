using Scootly.Application.Abstractions;

namespace Scootly.Application.Behaviors;

/// <summary>
/// <see cref="IIdempotentCommand"/> uygulayan komutları tekrara dayanıklı
/// kılar (60. gün deneyi — arayüz yaklaşımının çalışan hali).
/// </summary>
/// <remarks>
/// <para>
/// Öznitelik yaklaşımından iki temel farkı var:
/// </para>
/// <list type="number">
///   <item><b>Derleme zamanında görünür.</b> Bir komutun tekrara dayanıklı
///   olup olmadığı tip sisteminde yazılı; "hangi komutlar korumalı" sorusu
///   IDE'de tek tuşla cevaplanıyor.</item>
///   <item><b>HTTP'ye bağlı değil.</b> Aynı komut 54. günün arka plan
///   servisinden de çalıştırılabiliyor ve koruma orada da geçerli. Öznitelik
///   tabanlı süzgeç yalnızca MVC hattında çalışır — arka plan servisi onun
///   hiç uğramadığı bir yol.</item>
/// </list>
/// <para>
/// Deney amaçlı yazıldı ve henüz hiçbir komuta uygulanmadı; karar ADR 0020'de.
/// </para>
/// </remarks>
public sealed class IdempotencyBehavior
{
    /// <summary>Anahtarın hatırlanma süresi.</summary>
    /// <remarks>
    /// İstemcinin yeniden deneme penceresinden uzun, sonsuzdan kısa. Sonsuz
    /// saklamak, anahtar kümesinin sınırsız büyümesi demek olurdu.
    /// </remarks>
    public static readonly TimeSpan HatirlamaSuresi = TimeSpan.FromHours(1);

    private readonly IIdempotencyStore _store;

    public IdempotencyBehavior(IIdempotencyStore store)
    {
        _store = store;
    }

    /// <summary>
    /// Komutu çalıştırır; daha önce aynı anahtarla çalıştırıldıysa
    /// <paramref name="tekrarSonucu"/> döner ve <paramref name="calistir"/>
    /// HİÇ çağrılmaz.
    /// </summary>
    public async Task<TSonuc> CalistirAsync<TKomut, TSonuc>(
        TKomut command,
        Func<TKomut, CancellationToken, Task<TSonuc>> calistir,
        TSonuc tekrarSonucu,
        CancellationToken cancellationToken = default)
        where TKomut : IIdempotentCommand
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(calistir);

        if (string.IsNullOrWhiteSpace(command.IdempotencyKey))
        {
            throw new ArgumentException(
                "Idempotency anahtari bos olamaz.", nameof(command));
        }

        // Anahtar KOMUT TİPİYLE birlikte saklanıyor — süzgeçte yol ile
        // saklanmasının karşılığı. Aynı anahtarın iki farklı komut için
        // kullanılması iki farklı işlem.
        var kapsamliAnahtar = $"idem:{typeof(TKomut).Name}:{command.IdempotencyKey}";

        var ilkKez = await _store.IlkKezMiAsync(kapsamliAnahtar, HatirlamaSuresi, cancellationToken);

        if (!ilkKez)
        {
            return tekrarSonucu;
        }

        return await calistir(command, cancellationToken);
    }
}
